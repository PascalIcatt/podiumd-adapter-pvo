using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure
{
    public static class UrlRewriteExtensions
    {
        private static readonly ConcurrentDictionary<string, IReadOnlyCollection<Replacer>> s_cache = new();

        public static void UseUrlRewriter(this IApplicationBuilder applicationBuilder) => applicationBuilder.Use((context, next) =>
        {
            var inner = context.Features.Get<IHttpResponseBodyFeature>();
            if (inner == null)
            {
                return next(context);
            }

            var replacers = GetReplacers(context);

            if (replacers.Count == 0)
            {
                return next(context);
            }
            
            var wrapped = new UrlRewriteResponseBodyFeature(inner, replacers);

            context.Features.Set<IHttpResponseBodyFeature>(wrapped);


            return next(context);
        });

        private static IReadOnlyCollection<Replacer> GetReplacers(HttpContext context)
        {
            if (context?.Request == null) return [];

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var clients = context.RequestServices.GetServices<IEsuiteClientConfig>();

            return s_cache.GetOrAdd(context.Request.Host.Host, (host, tup) =>
            {
                var (clients, config, request) = tup;

                var requestUrl = new UriBuilder
                {
                    Host = host,
                    Port = request.Host.Port.GetValueOrDefault(),
                    Scheme = request.Scheme,
                };

                var replacers = new List<Replacer>();

                foreach (var item in clients)
                {
                    var targetUrl = config[item.ProxyBaseUrlConfigKey];
                    if (targetUrl == null) continue;
                    requestUrl.Path = item.RootUrl;
                    var sourceUrl = requestUrl.ToString();
                    var targetBytes = Encoding.UTF8.GetBytes(targetUrl);
                    var sourceBytes = Encoding.UTF8.GetBytes(sourceUrl);
                    replacers.Add(new(targetBytes, sourceBytes, targetUrl, sourceUrl));
                }

                return replacers;
            }, (clients, config, context.Request));
        }
    }
    public record Replacer(ReadOnlyMemory<byte> RemoteBytes, ReadOnlyMemory<byte> LocalBytes, string RemoteString, string LocalString);

    public class UrlRewriteResponseBodyFeature : IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _inner;

        public UrlRewriteResponseBodyFeature(IHttpResponseBodyFeature inner, IReadOnlyCollection<Replacer> replacers)
        {
            _inner = inner;
            Writer = new UrlPipeRewriter(inner.Writer, replacers);
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

    public class UrlPipeRewriter(PipeWriter inner, IReadOnlyCollection<Replacer> replacers) : DelegatingPipeWriter(inner)
    {
        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            if (replacers.Count == 0)
            {
                return await base.WriteAsync(source, cancellationToken);
            }

            IEnumerable<(int Index, Replacer Replacer)> GetIndices()
            {
                foreach (var replacer in replacers ?? [])
                {
                    var from = replacer.RemoteBytes.Span;
                    var to = replacer.LocalBytes.Span;
                    var index = source.Span.IndexOf(from);
                    if (index != -1) yield return (index, replacer);
                }
            }

            while (GetIndices().OrderBy(x => x.Index).FirstOrDefault() is { } tup && tup.Replacer != null)
            {
                var (index, replacer) = tup;
                if (index > 0)
                {
                    var result = await base.WriteAsync(source[..index], cancellationToken);
                    if (result.IsCanceled) return result;
                }
                var from = replacer.RemoteBytes;
                var to = replacer.LocalBytes;
                await base.WriteAsync(to, cancellationToken);
                var next = index + from.Length;
                source = source[next..];
            }

            return await base.WriteAsync(source, cancellationToken);
        }
    }

    public abstract class DelegatingPipeWriter(PipeWriter inner) : PipeWriter
    {
        public override void Advance(int bytes) => inner.Advance(bytes);

        public override void CancelPendingFlush() => inner.CancelPendingFlush();

        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => inner.FlushAsync(cancellationToken);

        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
    }
}
