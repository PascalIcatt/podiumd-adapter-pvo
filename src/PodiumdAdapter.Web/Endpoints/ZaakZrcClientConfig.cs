using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ZaakZrcClientConfig : IESuiteClientConfig
    {
        public string ProxyBasePath => "/zgw-apis-provider/zrc/api/v1";

        public string RootUrl => "/zaken/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
