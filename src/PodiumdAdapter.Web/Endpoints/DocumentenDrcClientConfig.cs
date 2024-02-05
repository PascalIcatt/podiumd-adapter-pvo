using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class DocumentenDrcClientConfig : IESuiteClientConfig
    {
        public string ProxyBasePath => "/zgw-apis-provider/drc/api/v1";

        public string RootUrl => "/documenten/api/v1";

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot, Func<HttpClient> getClient)
        {
        }
    }
}
