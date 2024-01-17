using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class KlantenClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_KLANTEN_BASE_URL";

        public string RootUrl => "/klanten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
