﻿using System.Net;

namespace PodiumdAdapter.Web.Test
{
    public class AuthTests(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
    {
        const string ContactmomentenBaseUrl = "/contactmomenten/api/v1";
        const string KlantenBaseUrl = "/klanten/api/v1";

        [Theory]
        [InlineData("/healthz", HttpStatusCode.OK)]
        [InlineData(ContactmomentenBaseUrl + "/contactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData(ContactmomentenBaseUrl + "/contactmomenten/a9aba7a1-5a91-4280-b079-dee5afad72e3", HttpStatusCode.Unauthorized)]
        [InlineData(ContactmomentenBaseUrl + "/klantcontactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData(ContactmomentenBaseUrl + "/objectcontactmomenten", HttpStatusCode.Unauthorized)]
        [InlineData(KlantenBaseUrl + "/klanten", HttpStatusCode.Unauthorized)]
        [InlineData(KlantenBaseUrl + "/klanten/a9aba7a1-5a91-4280-b079-dee5afad72e3", HttpStatusCode.Unauthorized, "PATCH")]
        public async Task Route_returns_expected_status_code_when_not_logged_in(string url, HttpStatusCode statusCode, string method = "GET")
        {
            using var client = webApplicationFactory.CreateClient();
            using var message = new HttpRequestMessage(new HttpMethod(method), url);
            using var result = await client.SendAsync(message);
            Assert.Equal(statusCode, result.StatusCode);
        }
    }
}
