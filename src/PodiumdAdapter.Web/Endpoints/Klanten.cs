using Generated.Esuite.KlantenClient;
using Generated.Esuite.KlantenClient.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Kiota.Abstractions;

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
            client.Klanten.GetAsync(x => x.QueryParameters = new() { SubjectNatuurlijkPersoonInpBsn = bsn }).WrapResult(logger);

        public static Task<IResult> Patch(
            ILogger<Klanten> logger,
            KlantenClient client,
            [FromRoute] Guid id,
            [FromBody] Klant klant
            ) =>
            client.Klanten[id].PatchAsync(klant).WrapResult(logger);


    }

    file static class Extensions
    {
        public static async Task<IResult> WrapResult<T>(this Task<T> task, ILogger logger)
        {
            try
            {
                var result = await task;
                return Results.Ok(result);
            }
            catch (Fout a)
            {
                return Results.Problem(a.Detail, a.Instance, a.Status, a.Title, a.Type, a.AdditionalData);
            }
            catch (ValidatieFout a)
            {
                return Results.Problem(a.Detail, a.Instance, a.Status, a.Title, a.Type, a.AdditionalData);
            }
            catch (ApiException a)
            {
                logger.LogError(a, "Api Exception");
                return Results.Problem(a.Message, statusCode: a.ResponseStatusCode);
            }
        }
    }
}
