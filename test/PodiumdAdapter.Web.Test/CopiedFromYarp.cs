// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace PodiumdAdapter.Web.Test;

internal enum StreamCopyResult
{
    Success,
    InputError,
    OutputError,
    Canceled
}

/// <summary>
/// A stream copier that captures errors.
/// </summary>
internal static class StreamCopier
{
    // Based on performance investigations, see https://github.com/microsoft/reverse-proxy/pull/330#issuecomment-758851852.
    private const int DefaultBufferSize = 65536;
    public const long UnknownLength = -1;

    public static ValueTask<(StreamCopyResult, Exception?)> CopyAsync(Stream input, Stream output, long promisedContentLength, CancellationToken cancellation)
        => CopyAsync(input, output, promisedContentLength, false, cancellation);

    public static async ValueTask<(StreamCopyResult, Exception?)> CopyAsync(Stream input, Stream output, long promisedContentLength, bool autoFlush, CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        var read = 0;
        long contentLength = 0;
        try
        {
            while (true)
            {
                read = 0;

                // Issue a zero-byte read to the input stream to defer buffer allocation until data is available.
                // Note that if the underlying stream does not supporting blocking on zero byte reads, then this will
                // complete immediately and won't save any memory, but will still function correctly.
                var zeroByteReadTask = input.ReadAsync(Memory<byte>.Empty, cancellation);
                if (zeroByteReadTask.IsCompletedSuccessfully)
                {
                    // Consume the ValueTask's result in case it is backed by an IValueTaskSource
                    _ = zeroByteReadTask.Result;
                }
                else
                {
                    // Take care not to return the same buffer to the pool twice in case zeroByteReadTask throws
                    var bufferToReturn = buffer;
                    buffer = null;
                    ArrayPool<byte>.Shared.Return(bufferToReturn);

                    await zeroByteReadTask;

                    buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                }

                read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                var str = Encoding.UTF8.GetString(buffer.AsSpan().Slice(0, read));
                contentLength += read;
                // Normally this is enforced by the server, but it could get out of sync if something in the proxy modified the body.
                if (promisedContentLength != UnknownLength && contentLength > promisedContentLength)
                {
                    return (StreamCopyResult.InputError, new InvalidOperationException("More bytes received than the specified Content-Length."));
                }


                // Success, reset the activity monitor.

                // End of the source stream.
                if (read == 0)
                {
                    if (promisedContentLength == UnknownLength || contentLength == promisedContentLength)
                    {
                        return (StreamCopyResult.Success, null);
                    }
                    else
                    {
                        // This can happen if something in the proxy consumes or modifies part or all of the request body before proxying.
                        return (StreamCopyResult.InputError,
                            new InvalidOperationException($"Sent {contentLength} request content bytes, but Content-Length promised {promisedContentLength}."));
                    }
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                if (autoFlush)
                {
                    // HttpClient doesn't always flush outgoing data unless the buffer is full or the caller asks.
                    // This is a problem for streaming protocols like WebSockets and gRPC.
                    await output.FlushAsync(cancellation);
                }


                // Success, reset the activity monitor.
            }
        }
        catch (Exception ex)
        {
            // If the activity timeout triggered while reading or writing, blame the sender or receiver.
            var result = read == 0 ? StreamCopyResult.InputError : StreamCopyResult.OutputError;
            return (result, ex);
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
