using Microsoft.AspNetCore.Mvc.Testing;

namespace PodiumdAdapter.Web.Test
{
    public class HealthCheckTest(WebApplicationFactory<Program> webApplicationFactory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task Healthz_is_reachable_anonymously()
        {
            using var client = webApplicationFactory.CreateClient();
            using var result = await client.GetAsync("/healthz");
            Assert.True(result.IsSuccessStatusCode);
        }
    }
}
