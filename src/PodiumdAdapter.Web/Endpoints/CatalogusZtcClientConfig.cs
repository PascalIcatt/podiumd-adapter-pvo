using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class CatalogusZtcClientConfig : IESuiteClientConfig
    {
        public string ProxyBasePath => "/zgw-apis-provider/ztc/api/v1";

        public string RootUrl => "/catalogi/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
