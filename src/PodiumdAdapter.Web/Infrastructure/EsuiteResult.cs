using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace PodiumdAdapter.Web
{
    public static class EsuiteResult
    {
        private static readonly JsonSerializerOptions s_options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static IResult Map<T, TOut>(this Task<T?> task, Func<T, TOut> mapper) where T : IParsable
            => new MappedParsableResult<T, TOut>(task, mapper);

        public static IResult ToResult<T>(this Task<T?> task) where T : IParsable
            => new MappedParsableResult<T, T>(task, x=> x);

        private class MappedParsableResult<T, TOut>(Task<T?> task, Func<T, TOut> mapper) : IResult where T : IParsable
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<MappedParsableResult<T, TOut>>>();
                var result = await GetResult(task, mapper, logger);
                await result.ExecuteAsync(httpContext);
            }

            private static async Task<IResult> GetResult(Task<T?> task, Func<T, TOut> mapper, ILogger logger)
            {
                try
                {
                    var t = await task;
                    if (t == null) return Results.NoContent();
                    var mapped = mapper(t);
                    var json = JsonNode.Parse(JsonSerializer.Serialize(mapped, s_options));
                    Clean(json);
                    return Results.Ok(json);
                }
                catch (ApiException a)
                {
                    logger.LogError(a, "Api Exception");
                    var status = a.ResponseStatusCode == default ? 500 : a.ResponseStatusCode;
                    return a is IParsable parsable
                        ? new ParsableResult(parsable, status)
                        : Results.Problem(a.Message, statusCode: status);
                }
            }
        }

        private static void Clean(JsonNode node)
        {
            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    Clean(item);
                }
            }
            if (node is JsonObject obj)
            {
                var props = obj.ToList();
                foreach (var (key,value) in props)
                {
                    if(key == "additionalData")
                    {
                        obj.Remove(key);
                        if(value is JsonObject additionalData)
                        {
                            foreach (var dataItem in additionalData)
                            {
                                obj[dataItem.Key] = dataItem.Value;
                            }
                        }
                    }
                    Clean(value);
                }
            }
        }

        private class ParsableResult(IParsable parsable, int status) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                var response = httpContext.Response;
                response.StatusCode = status;
                response.ContentType = "application/json";
                var writer = new JsonSerializationWriter();
                writer.writer.WriteStartObject();
                parsable.Serialize(writer);
                writer.writer.WriteEndObject();
                return writer.GetSerializedContent().CopyToAsync(response.Body);
            }
        }
    }
}
