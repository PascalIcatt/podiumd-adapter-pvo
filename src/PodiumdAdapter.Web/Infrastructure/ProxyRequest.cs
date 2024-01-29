using System.Text.Json.Nodes;

namespace PodiumdAdapter.Web
{
    public delegate ValueTask Modify(JsonNode node, CancellationToken token = default);

    public class ProxyRequest
    {
        public HttpMethod? Method { get; set; }
        public required string Url { get; set; }
        public Modify? ModifyRequestBody { get; set; }
        public Modify? ModifyResponseBody { get; set; }
    }
}
