using System.Net;

namespace PodiumdAdapter.Web.Test
{
    public class PostZaakContactmomentenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Responds_with_no_content()
        {
            using var client = factory.CreateClient();
            factory.SetZgwToken(client);
            using var response = await client.PostAsync("/zaken/api/v1/zaakcontactmomenten", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
