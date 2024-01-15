namespace PodiumdAdapter.Web.Test;

public class KlantenTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    const string KlantenBaseUrl = "/klanten/api/v1";

    [Fact]
    public async Task Proxy_returns_correct_response_when_logged_in()
    {
        const string Input = "{'name' : 'Test McGee'}";
        const string Path = "/klanten";
        using var client = factory.CreateClient();
        factory.Login(client);
        factory.MockHttpMessageHandler
            .When(factory.ESUITE_KLANTEN_BASE_URL + Path)
            .Respond("application/json", Input);

        using var response = await client.GetAsync(KlantenBaseUrl + Path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(Input, body);
    }
}
