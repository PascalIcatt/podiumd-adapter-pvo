using Generated.Esuite.ContactmomentenClient.Models;
using PodiumdAdapter.Web.Test.Infrastructure;

namespace PodiumdAdapter.Web.Test;

[UsesVerify]
public class ContactmomentenTest(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetAll()
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

        using var result = await client.GetStreamAsync("/contactmomenten");
        await VerifyJson(result);
    }

}
