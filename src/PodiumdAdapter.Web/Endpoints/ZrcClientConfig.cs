using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ZrcClientConfig : IEsuiteClientConfig
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_ZRC_BASE_URL";

        public string RootUrl => "/zaken/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
