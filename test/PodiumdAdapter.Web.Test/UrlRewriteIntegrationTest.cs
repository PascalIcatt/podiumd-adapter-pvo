namespace PodiumdAdapter.Web.Test
{
    public class UrlRewriteIntegrationTest(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Urls_in_body_are_rewritten_in_both_directions_when_proxying_calls()
        {
            const string BodyWithLocalUrl = "{'replace me' : 'http://localhost/zaken/api/v1/'}";
            const string BodyWithRemoteUrl = "{'replace me' : 'https://localhost:12345/zgw-apis-provider/zrc/api/v1/'}";

            var path = "/klanten/" + Guid.NewGuid(); ;
            using var client = factory.CreateClient();
            factory.SetZgwToken(client);

            // setup the mock http message handler (YARP uses this to proxy calls)
            //
            // it expects a call to the specified url, with a body containing the Remote url
            // if this happens, it means the url in the request body is succesfully rewritten from the local url to the remote url
            // in that case, the handler sends a response body with the Remote url in it
            //
            // if in the end we receive a response body with the Local url in it,
            // this means the url is again succesfully rewritten on the way back
            var request = factory.MockHttpMessageHandler
                .Expect(HttpMethod.Patch, factory.ESUITE_BASE_URL + "/klanten-api-provider/api/v1" + path)
                .WithContent(BodyWithRemoteUrl)
                .Respond("application/json", BodyWithRemoteUrl);

            using var content = new StringContent(BodyWithLocalUrl, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            using var response = await client.PatchAsync("/klanten/api/v1" + path, content);
            var body = await response.Content.ReadAsStringAsync();

            var timesTheMockHttpMessageHandlerWasTriggered = factory.MockHttpMessageHandler.GetMatchCount(request);
            Assert.Equal(1, timesTheMockHttpMessageHandlerWasTriggered);

            Assert.True(response.IsSuccessStatusCode);
            
            
            Assert.Equal(BodyWithLocalUrl, body);
        }
    }
}
