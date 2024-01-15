using Generated.Esuite.KlantenClient;
using Generated.Esuite.KlantenClient.Models;
using Microsoft.AspNetCore.Mvc;

namespace PodiumdAdapter.Web.Endpoints
{
    public class Klanten
    {
        public static void Api(IEndpointRouteBuilder endpointRouteBuilder)
        {
            var root = endpointRouteBuilder.MapGroup("/klanten/api/v1");
            var klanten = root.MapGroup("/klanten");
            klanten.MapGet("/", Get);
            klanten.MapPatch("/{id:guid}", Patch);
        }

        public static Task<IResult> Get(
            ILogger<Klanten> logger,
            KlantenClient client,
            [FromQuery(Name = "subjectNatuurlijkPersoon__inpBsn")] string? bsn
            ) =>
            client.Klanten
                .GetAsync(x => x.QueryParameters = new() { SubjectNatuurlijkPersoonInpBsn = bsn })
                .ToResult(logger);

        public static Task<IResult> Patch(
            ILogger<Klanten> logger,
            KlantenClient client,
            [FromRoute] Guid id,
            [FromBody] Klant klant
            ) =>
            client.Klanten[id]
                .PatchAsync(klant)
                .ToResult(logger);


    }
}
