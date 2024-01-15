using Generated.Esuite.ZtcClient;
using Microsoft.Kiota.Abstractions;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ZtcClientConfig : IEsuiteClientConfig<ZtcClient>
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_ZTC_BASE_URL";

        public string RootUrl => "/catalogi/api/v1";

        public ZtcClient CreateClient(IRequestAdapter requestAdapter) => new(requestAdapter);

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot)
        {
        }
    }
}
