using System.Net.Http.Json;

namespace PodiumdAdapter.Web.Test
{
    public class InterneTaakTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Post_response_contains_correct_url()
        {
            const string baseUrl = "/api/v2/objects";
            var cmId = Guid.NewGuid().ToString();
            var cmUrl = "https://www.google.nl/" + cmId;
            var expectedContent = $$$"""
            {"record":{"data":{"contactmoment":"{{{cmUrl}}}"}},"url":"http://localhost{{{baseUrl}}}/{{{cmId}}}","uuid":"{{{cmId}}}"}
            """;

            using var client = factory.CreateClient();
            factory.SetObjectenToken(client);

            using var response = await client.PostAsJsonAsync(baseUrl, new
            {
                record = new
                {
                    data = new
                    {
                        contactmoment = cmUrl
                    }
                }
            });
            
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedContent, content);
        }
    }
}
