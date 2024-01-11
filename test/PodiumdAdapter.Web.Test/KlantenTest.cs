using System.Net.Http.Json;
using Argon;
using Generated.Esuite.KlantenClient.Models;
using PodiumdAdapter.Web.Test.Infrastructure;

namespace PodiumdAdapter.Web.Test;

[UsesVerify]
public class KlantenTest(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetAll()
    {
        var clientResponse = new KlantResults
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

        using var result = await client.GetStreamAsync("/klanten");
        await VerifyJson(result);
    }

    [Fact]
    public async Task Patch()
    {
        var klant = new Klant();
        var id = Guid.NewGuid();
        webApplicationFactory.SetEsuiteResponse(klant);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var r1 = await client.PatchAsJsonAsync("/klanten/" + id, klant);
        var r2 = await r1.Content.ReadAsStringAsync();

        using var response = await client.PatchAsJsonAsync("/klanten/" + id, klant);
        using var result = await response.Content.ReadAsStreamAsync();
        try
        {
            await VerifyJson(result);
        }
        catch (JsonReaderException e)
        {
            throw;
        }
    }
}
