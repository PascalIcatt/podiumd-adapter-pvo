using System.Text.Json;
using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web
{
    public static class ProxyResultExtensions
    {
        private static readonly JsonSerializerOptions s_options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static IResult ProxyResult(this HttpClient client, Func<HttpRequestMessage> message, Func<JsonNode, Task>? modify = null)
            => new ProxyResult(client, message, modify);

        public static async Task<JsonNode?> JsonAsync(this HttpClient client, Func<HttpRequestMessage> message)
        {
            using var request = message();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync();
            var node = await JsonNode.ParseAsync(stream);
            return node;
        }

        public static bool TryParsePagination(this JsonNode? node, out JsonArray result)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue("results", out var r) || r is not JsonArray arr)
            {
                result = null!;
                return false;
            }
            result = arr;
            return true;
        }
    }

    public class ProxyResult(HttpClient client, Func<HttpRequestMessage> messageFactory, Func<JsonNode, Task>? modify = null) : IResult
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
            await using var str = await response.Content.ReadAsStreamAsync(token);
            if (!response.IsSuccessStatusCode || modify == null)
            {
                await str.CopyToAsync(httpContext.Response.Body, token);
                return;
            }
            var node = await JsonNode.ParseAsync(str, cancellationToken: token);
            if (node == null) return;
            try
            {
                await modify(node);
            }
            catch (Exception)
            {
            }
            await httpContext.Response.WriteAsJsonAsync(node, token);
        }
    }
}
