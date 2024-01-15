using Generated.Esuite.KlantenClient;
using Microsoft.Kiota.Abstractions;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class KlantenClientConfig : IEsuiteClientConfig<KlantenClient>
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_KLANTEN_BASE_URL";

        public string RootUrl => "/klanten/api/v1";

        public KlantenClient CreateClient(IRequestAdapter requestAdapter) => new(requestAdapter);

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot)
        {
        }
    }
}
