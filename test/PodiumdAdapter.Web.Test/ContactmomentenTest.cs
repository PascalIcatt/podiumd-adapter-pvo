using System.Net.Http.Json;
using Generated.Esuite.ContactmomentenClient.Models;
using PodiumdAdapter.Web.Test.Infrastructure;

namespace PodiumdAdapter.Web.Test;

public class ContactmomentenTest(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Test()
    {
        var clientResponse = new ContactmomentResults
        {
            Count = 1,
            Results =
            [
                new()
            ]
        };

        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        var result = await client.GetFromJsonAsync<ContactmomentResults>("/contactmomenten");
        Assert.Equal(clientResponse.Results.Count, result?.Results?.Count);
    }

}
