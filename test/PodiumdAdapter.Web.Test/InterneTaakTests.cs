﻿using System.Net.Http.Json;

namespace PodiumdAdapter.Web.Test
{
    public class InterneTaakTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Test()
        {
            const string baseUrl = "/internetaak/api/v2/objects";
            var cmId = Guid.NewGuid().ToString();
            var cmUrl = "https://www.google.nl/" + cmId;
            var expectedContent = $$"""
            {"url":"http://localhost{{baseUrl}}/{{cmId}}"}
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
