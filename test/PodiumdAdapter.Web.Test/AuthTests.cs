using System.Net;

namespace PodiumdAdapter.Web.Test
{
    public class AuthTests(WebApplicationFactory<Program> webApplicationFactory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Theory]
        [InlineData("/healthz", HttpStatusCode.OK)]
        [InlineData("/contactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData("/contactmomenten/a9aba7a1-5a91-4280-b079-dee5afad72e3", HttpStatusCode.Unauthorized)]
        [InlineData("/klantcontactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData("/objectcontactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData("/klanten", HttpStatusCode.Unauthorized)]
        [InlineData("/klanten/a9aba7a1-5a91-4280-b079-dee5afad72e3", HttpStatusCode.Unauthorized, "PATCH")]
        public async Task Route_returns_expected_status_code_when_not_logged_in(string url, HttpStatusCode statusCode, string method = "GET")
        {
            using var client = webApplicationFactory.CreateClient();
            using var message = new HttpRequestMessage(new HttpMethod(method), url);
            using var result = await client.SendAsync(message);
            Assert.Equal(statusCode, result.StatusCode);
        }
    }
}
