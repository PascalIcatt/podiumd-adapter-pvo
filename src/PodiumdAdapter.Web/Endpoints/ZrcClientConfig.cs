using Generated.Esuite.ZrcClient;
using Microsoft.Kiota.Abstractions;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ZrcClientConfig : IEsuiteClientConfig<ZrcClient>
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_ZRC_BASE_URL";

        public string RootUrl => "/zaken/api/v1";

        public ZrcClient CreateClient(IRequestAdapter requestAdapter) => new(requestAdapter);

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot)
        {
        }
    }
}
