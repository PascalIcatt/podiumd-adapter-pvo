using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class KlantenClientConfig : IESuiteClientConfig
    {
        public string ProxyBasePath => "/klanten-api-provider/api/v1";

        public string RootUrl => "/klanten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
