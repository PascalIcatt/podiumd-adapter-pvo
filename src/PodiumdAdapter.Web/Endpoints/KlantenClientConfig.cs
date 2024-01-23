using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class KlantenClientConfig : IESuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_KLANTEN_BASE_URL";

        public string RootUrl => "/klanten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            // tot het GET request in de API geimplementeerd is, doen we hier een patch met een leeg object als body
            clientRoot.MapGet("/klanten/{id:guid}", (Guid id) =>
            {
                var client = getClient();
                HttpRequestMessage CreateRequest() => new(HttpMethod.Patch, "klanten/" + id)
                {
                    Content = JsonContent.Create(new { })
                };
                return client.ProxyResult(CreateRequest);
            });
        }
    }
}
