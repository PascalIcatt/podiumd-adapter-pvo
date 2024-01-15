using Generated.Esuite.KlantenClient.Models;

namespace PodiumdAdapter.Web.Test;

[UsesVerify]
public class KlantenTest(CustomWebApplicationFactory webApplicationFactory) : IClassFixture<CustomWebApplicationFactory>
{
    const string BaseUri = "/klanten/api/v1";

    [Fact]
    public async Task GetAll()
    {
        var clientResponse = new KlantResults
        {
            Count = 1,
            Results =
            [
                new()
                {
                    Emailadres = "email",
                    Telefoonnummer = "telefoon",
                }
            ]
        };

        webApplicationFactory.SetEsuiteResponse(clientResponse);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var result = await client.GetStreamAsync(BaseUri + "/klanten");
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

        using var r1 = await client.PatchAsJsonAsync(BaseUri + "/klanten/" + id, klant);
        var r2 = await r1.Content.ReadAsStringAsync();

        using var response = await client.PatchAsJsonAsync(BaseUri + "/klanten/" + id, klant);
        using var result = await response.Content.ReadAsStreamAsync();
        await VerifyJson(result);
    }

    [Fact]
    public async Task ValidatieFout_case()
    {
        var validatieError = new ValidatieFout
        {
            Code = ValidatieFout_code.NOT_FOUND,
            Detail = "Detail",
            Title = "Title",
            Instance = "Instance",
            Status = 404,
            Type = "Type",
            ResponseStatusCode = 404,
            AdditionalData = new Dictionary<string, object>
            {
                ["extra"] = "data"
            }
        };

        webApplicationFactory.SetEsuiteError<KlantResults>(validatieError);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var response = await client.GetAsync(BaseUri + "/klanten");
        using var result = await response.Content.ReadAsStreamAsync();
        await VerifyJson(result);
    }

    [Fact]
    public async Task Fout_case()
    {
        var error = new Fout
        {
            Code = Fout_code.NOT_FOUND,
            Detail = "Detail",
            Title = "Title",
            Instance = "Instance",
            Status = 404,
            Type = "Type",
            AdditionalData = new Dictionary<string, object>
            {
                ["extra"] = "data"
            }
        };

        webApplicationFactory.SetEsuiteError<KlantResults>(error);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var response = await client.GetAsync(BaseUri + "/klanten");
        using var result = await response.Content.ReadAsStreamAsync();
        await VerifyJson(result);
    }

    [Fact]
    public async Task ApiException_case()
    {
        var error = new ApiException("Message")
        {
            ResponseStatusCode = 404,
        };

        webApplicationFactory.SetEsuiteError<KlantResults>(error);

        using var client = webApplicationFactory.CreateClient();
        webApplicationFactory.Login(client);

        using var response = await client.GetAsync(BaseUri + "/klanten");
        using var result = await response.Content.ReadAsStreamAsync();
        await VerifyJson(result);
    }
}
