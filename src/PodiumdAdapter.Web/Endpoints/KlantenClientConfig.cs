using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class KlantenClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_KLANTEN_BASE_URL";

        public string RootUrl => "/klanten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
            clientRoot.MapGet("/klanten/{id:guid}", (Guid id) =>
            {
                var client = getClient();
                var request = () => new HttpRequestMessage(HttpMethod.Patch, "klanten/" + id) { Content = JsonContent.Create(new { }) };
                return client.ProxyResult(request);
            });
        }
    }
}
