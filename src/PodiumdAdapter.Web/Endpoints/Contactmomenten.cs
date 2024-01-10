using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.ContactmomentenClient.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Kiota.Abstractions;
using static Generated.Esuite.ContactmomentenClient.Contactmomenten.ContactmomentenRequestBuilder;
using static Generated.Esuite.ContactmomentenClient.Klantcontactmomenten.KlantcontactmomentenRequestBuilder;
using static Generated.Esuite.ContactmomentenClient.Objectcontactmomenten.ObjectcontactmomentenRequestBuilder;

namespace PodiumdAdapter.Web.Endpoints
{
    public class Contactmomenten
    {
        public static void Api(IEndpointRouteBuilder endpointRouteBuilder)
        {
            var contactmomenten = endpointRouteBuilder.MapGroup("/contactmomenten");
            contactmomenten.MapGet("/", Get);
            contactmomenten.MapGet("/{id:guid}", GetById);
            contactmomenten.MapPost("/", Post);

            endpointRouteBuilder.MapGet("/klantcontactmomenten", KlantContactmomenten);
            endpointRouteBuilder.MapGet("/objectcontactmomenten", ObjectContactmomenten);
        }

        public static Task<IResult> Get(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] ContactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Contactmomenten.GetAsync(x => x.QueryParameters = query).WrapResult(logger);

        public static Task<IResult> Post(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            Contactmoment contactmoment
            ) =>
            client.Contactmomenten.PostAsync(contactmoment).WrapResult(logger);

        public static Task<IResult> GetById(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [FromRoute] Guid id
            ) =>
            client.Contactmomenten[id].GetAsync().WrapResult(logger);

        public static Task<IResult> KlantContactmomenten(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] KlantcontactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Klantcontactmomenten.GetAsync(x => x.QueryParameters = query).WrapResult(logger);

        public static Task<IResult> ObjectContactmomenten(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] ObjectcontactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Objectcontactmomenten.GetAsync(x => x.QueryParameters = query).WrapResult(logger);

        
    }

    static file class Extensions
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
