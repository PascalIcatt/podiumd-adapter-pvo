using System.Net.Http.Json;
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

    [Fact]
    public async Task GetById()
    {
        var clientResponse = new Contactmoment();
        var id = Guid.NewGuid();
        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var result = await client.GetStreamAsync("/contactmomenten/" + id);
        await VerifyJson(result);
    }

    [Fact]
    public async Task Post()
    {
        var contactmoment = new Contactmoment();
        webApplicationFactory.SetEsuiteResponse(contactmoment);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var response = await client.PostAsJsonAsync("/contactmomenten", contactmoment);
        using var result = await response.Content.ReadAsStreamAsync();
        await VerifyJson(result);
    }

    [Fact]
    public async Task KlantContactmomenten()
    {
        var clientResponse = new KlantcontactmomentResults
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

        using var result = await client.GetStreamAsync("/klantcontactmomenten");
        await VerifyJson(result);
    }

    [Fact]
    public async Task ObjectContactmomenten()
    {
        var clientResponse = new ObjectcontactmomentResults
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

        using var result = await client.GetStreamAsync("/objectcontactmomenten");
        await VerifyJson(result);
    }
}
