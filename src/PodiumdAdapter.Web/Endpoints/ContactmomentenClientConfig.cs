using Generated.Esuite.ContactmomentenClient;
using Microsoft.Kiota.Abstractions;
using PodiumdAdapter.Web.Infrastructure;

namespace PodiumdAdapter.Web.Endpoints
{
    public class ContactmomentenClientConfig : IEsuiteClientConfig<ContactmomentenClient>
    {
        public string ProxyBaseUrlConfigKey => "ESUITE_CONTACTMOMENTEN_BASE_URL";

        public string RootUrl => "/contactmomenten/api/v1";

        public ContactmomentenClient CreateClient(IRequestAdapter requestAdapter) => new(requestAdapter);

        public void MapCustomEndpoints(IEndpointRouteBuilder clientRoot)
        {
        }
    }
}
