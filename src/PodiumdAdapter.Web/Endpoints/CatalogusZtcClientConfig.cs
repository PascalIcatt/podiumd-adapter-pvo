using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class CatalogusZtcClientConfig : IESuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_ZTC_BASE_URL";

        public string RootUrl => "/catalogi/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
