using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class UrlRewriteExtensions
    {
        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var inner = context.Features.Get<IHttpResponseBodyFeature>();
            if (inner != null)
            {
                var accessor = context.RequestServices.GetRequiredService<IHttpContextAccessor>();
                var config = context.RequestServices.GetRequiredService<IConfiguration>();
                var clients = context.RequestServices.GetServices<IESuiteClientConfig>();
                var wrapped = new UrlRewriteFeature(inner, accessor, config, clients);
                context.Features.Set<IHttpResponseBodyFeature>(wrapped);
            }
            return next(context);
        });
    }

    public abstract class DelegatingPipewriter(PipeWriter inner) : PipeWriter
    {
        public override void Advance(int bytes) => inner.Advance(bytes);

        public override void CancelPendingFlush() => inner.CancelPendingFlush();

        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => inner.FlushAsync(cancellationToken);

        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
    }

    public class UrlPipeRewriter(PipeWriter inner, IHttpContextAccessor httpContextAccessor, IConfiguration config, IEnumerable<IESuiteClientConfig> clients) : DelegatingPipewriter(inner)
    {
        private static readonly ConcurrentDictionary<string, IReadOnlyCollection<(byte[], byte[])>> s_cache = new();

        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            var replacers = GetReplacers();

            if (replacers.Count == 0)
            {
                return await base.WriteAsync(source, cancellationToken);
            }

            IEnumerable<(int Index, byte[] From, byte[] To)> GetIndices()
            {
                foreach (var (from, to) in replacers ?? [])
                {
                    var index = source.Span.IndexOf(from);
                    if (index != -1) yield return (index, from, to);
                }
            }

            while (GetIndices().OrderBy(x => x.Index).FirstOrDefault() is { } tup && tup.From != null)
            {
                var (index, from, to) = tup;
                if (index > 0)
                {
                    var result = await base.WriteAsync(source[..index], cancellationToken);
                    if (result.IsCanceled) return result;
                }
                await base.WriteAsync(to, cancellationToken);
                var next = index + from.Length;
                source = source[next..];
            }

            return await base.WriteAsync(source, cancellationToken);
        }

        private IReadOnlyCollection<(byte[], byte[])> GetReplacers()
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request == null) return [];

            return s_cache.GetOrAdd(request.Host.Host, (_, tup) =>
            {
                var (clients, config, request) = tup;

                var requestUrl = new UriBuilder
                {
                    Host = request.Host.Host,
                    Port = request.Host.Port.GetValueOrDefault(),
                    Scheme = request.Scheme,
                };

                var replacers = new List<(byte[], byte[])>();

                foreach (var item in clients)
                {
                    var targetUrl = config[item.ProxyBaseUrlConfigKey];
                    if (targetUrl == null) continue;
                    requestUrl.Path = item.RootUrl;
                    var sourceUrl = requestUrl.ToString();
                    replacers.Add((Encoding.UTF8.GetBytes(targetUrl), Encoding.UTF8.GetBytes(sourceUrl)));
                }

                return replacers;
            }, (clients, config, request));
        }
    }

    public class UrlRewriteFeature : IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _inner;

        public UrlRewriteFeature(IHttpResponseBodyFeature inner, IHttpContextAccessor httpContextAccessor, IConfiguration config, IEnumerable<IESuiteClientConfig> clients)
        {
            _inner = inner;
            Writer = new UrlPipeRewriter(inner.Writer, httpContextAccessor, config, clients);
            Stream = Writer.AsStream();
        }

        public Stream Stream { get; }

        public PipeWriter Writer { get; }

        public Task CompleteAsync() => _inner.CompleteAsync();

        public void DisableBuffering() => _inner.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
            => _inner.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
    }


}
