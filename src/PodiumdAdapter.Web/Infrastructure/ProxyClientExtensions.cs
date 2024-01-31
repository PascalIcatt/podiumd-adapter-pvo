using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web
{

    public static class ProxyClientExtensions
    {
        public static IResult ProxyResult(this HttpClient client, ProxyRequest request)
            => new ProxyResult(client, request);

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

    public class ProxyResult(HttpClient client, ProxyRequest request) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var token = httpContext.RequestAborted;
            var method = request.Method ?? new HttpMethod(httpContext.Request.Method);
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<ProxyResult>>();

            using var message = new HttpRequestMessage(method, request.Url);

            if (request.ModifyRequestBody == null)
            {
                message.Content = new StreamContent(httpContext.Request.Body);
            }
            else
            {
                var json = await JsonNode.ParseAsync(httpContext.Request.Body, cancellationToken: token);
                if (json != null)
                {
                    try
                    {
                        await request.ModifyRequestBody(json, token);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error while trying to modify request body. Ignoring and sending original request");
                    }
                }
                message.Content = JsonContent.Create(json);
            }

            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
            httpContext.Response.StatusCode = (int)response.StatusCode;
            foreach (var item in response.Headers)
            {
                // deze header geeft aan of de content 'chunked' is. maar die waarde kunnen we niet overnemen,
                // die is namelijk van hoe we zelf hieronder de response opbouwen.
                if (item.Key.Equals("transfer-encoding", StringComparison.OrdinalIgnoreCase)) continue;
                httpContext.Response.Headers[item.Key] = new(item.Value.ToArray());
            }
            foreach (var item in response.Content.Headers)
            {
                if (item.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase)) continue;
                httpContext.Response.Headers[item.Key] = new(item.Value.ToArray());
            }
            if (!response.IsSuccessStatusCode || request.ModifyResponseBody == null)
            {
                await response.Content.CopyToAsync(httpContext.Response.Body, token);
                return;
            }
            await using var str = await response.Content.ReadAsStreamAsync(token);
            var node = await JsonNode.ParseAsync(str, cancellationToken: token);
            if (node == null) return;
            try
            {
                await request.ModifyResponseBody(node, token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while trying to modify response body. Ignoring and sending original response");
            }
            await httpContext.Response.WriteAsJsonAsync(node, token);
        }
    }
}
