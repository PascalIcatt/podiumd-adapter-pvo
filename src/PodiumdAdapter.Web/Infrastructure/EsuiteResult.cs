using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace PodiumdAdapter.Web
{
    public static class EsuiteResult
    {
        public static IResult Map<T, TOut>(this Task<T?> task, Func<T, TOut> mapper) where T : IParsable
            => new MappedParsableResult<T, TOut>(task, mapper);

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
                    return t == null
                        ? Results.NoContent()
                        : Results.Ok(mapper(t));
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
