using Generated.Esuite.ContactmomentenClient.Models;

namespace PodiumdAdapter.Web.Test;

[UsesVerify]
public class ContactmomentenTest(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
{
    const string BaseUri = "/contactmomenten/api/v1";

    [Fact]
    public async Task GetAll()
    {
        var clientResponse = new ContactmomentResults
        {
            Count = 1,
            Results =
            [
                new Contactmoment
                {
                    Kanaal = "Kanaal",
                }
            ]
        };

        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var result = await client.GetStreamAsync(BaseUri + "/contactmomenten");
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

        using var result = await client.GetStreamAsync(BaseUri + "/contactmomenten/" + id);
        await VerifyJson(result);
    }

    [Fact]
    public async Task Post()
    {
        var contactmoment = new Contactmoment();
        webApplicationFactory.SetEsuiteResponse(contactmoment);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var response = await client.PostAsJsonAsync(BaseUri + "/contactmomenten", contactmoment);
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
                new Klantcontactmoment
                {
                    Contactmoment = "Contactmoment",
                    Klant = "Klant"
                }
            ]
        };

        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var result = await client.GetStreamAsync(BaseUri + "/klantcontactmomenten");
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
                new Objectcontactmoment
                {
                    Contactmoment = "Contactmoment",
                    Object = "Object",
                    ObjectType = Objectcontactmoment_objectType.Zaak,
                }
            ]
        };

        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var result = await client.GetStreamAsync(BaseUri + "/objectcontactmomenten");
        await VerifyJson(result);
    }
}
