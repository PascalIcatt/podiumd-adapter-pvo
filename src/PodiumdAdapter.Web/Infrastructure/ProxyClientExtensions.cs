using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web
{
    public delegate Task Modify(JsonNode node, CancellationToken token = default);

    public static class ProxyClientExtensions
    {
        public static IResult ProxyResult(this HttpClient client, Func<HttpRequestMessage> message, Modify? modify = null)
            => new ProxyResult(client, message, modify);

        public static IResult ProxyResult(this HttpClient client, string url, Modify? modify = null)
            => client.ProxyResult(HttpMethod.Get, url, modify);

        public static IResult ProxyResult(this HttpClient client, HttpMethod method, string url, Modify? modify = null)
            => client.ProxyResult(() => new(method, url), modify);

        public static Task<JsonNode?> JsonAsync(this HttpClient client, string url, CancellationToken token)
            => client.JsonAsync(HttpMethod.Get, url, token);

        public static Task<JsonNode?> JsonAsync(this HttpClient client, HttpMethod method, string url, CancellationToken token)
            => client.JsonAsync(() => new(method, url), token);

        public static async Task<JsonNode?> JsonAsync(this HttpClient client, Func<HttpRequestMessage> message, CancellationToken token)
        {
            using var request = message();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var node = await JsonNode.ParseAsync(stream, cancellationToken: token);
            return node;
        }

        public static bool TryParsePagination(this JsonNode? node, [NotNullWhen(true)] out JsonArray result, out string? next)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue("results", out var r) || r is not JsonArray arr)
            {
                result = null!;
                next = default;
                return false;
            }

            result = arr;

            next = !obj.TryGetPropertyValue("next", out var nextProp) || nextProp is not JsonValue nextValue
                ? null
                : nextValue.ToString();

            return result != null;
        }

        public static bool TryParsePagination(this JsonNode? node, out JsonArray result)
            => node.TryParsePagination(out result, out _);
    }

    public class ProxyResult(HttpClient client, Func<HttpRequestMessage> messageFactory, Modify? modify = null) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var token = httpContext.RequestAborted;
            using var message = messageFactory();
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
            httpContext.Response.StatusCode = (int)response.StatusCode;
            foreach (var item in response.Headers)
            {
                httpContext.Response.Headers[item.Key] = new(item.Value.ToArray());
            }
            foreach (var item in response.Content.Headers)
            {
                if (item.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase)) continue;
                httpContext.Response.Headers[item.Key] = new(item.Value.ToArray());
            }
            if (!response.IsSuccessStatusCode || modify == null)
            {
                await response.Content.CopyToAsync(httpContext.Response.Body, token);
                return;
            }
            await using var str = await response.Content.ReadAsStreamAsync(token);
            var node = await JsonNode.ParseAsync(str, cancellationToken: token);
            if (node == null) return;
            try
            {
                await modify(node, token);
            }
            catch (Exception)
            {
            }
            await httpContext.Response.WriteAsJsonAsync(node, token);
        }
    }
}
