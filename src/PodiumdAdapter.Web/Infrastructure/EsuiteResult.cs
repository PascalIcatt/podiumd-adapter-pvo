
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace PodiumdAdapter.Web
{
    public static class Ext
    {
        public static async Task<IResult> ToResult<T>(this Task<T?> task, ILogger logger) where T : IParsable
        {
            try
            {
                var result = await task;
                return result == null
                    ? Results.NoContent()
                    // hier gebruiken we niet IParsable, want kiota negeert read-only properties bij het serializeren
                    : Results.Ok(result);
            }
            catch (ApiException a)
            {
                logger.LogError(a, "Api Exception");
                var status = a.ResponseStatusCode == default ? 500 : a.ResponseStatusCode;
                return a is IParsable p
                    ? new EsuiteResultInternal<IParsable>(p, status)
                    : Results.Problem(a.Message, statusCode: status);
            }
        }


        private class EsuiteResultInternal<T>(T parsable, int status = 200) : IResult where T : IParsable
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = status;
                httpContext.Response.ContentType = "application/json";
                var writer = new JsonSerializationWriter();
                writer.writer.WriteStartObject();
                parsable.Serialize(writer);
                writer.writer.WriteEndObject();
                return writer.GetSerializedContent().CopyToAsync(httpContext.Response.Body);
            }
        }
    }
}
