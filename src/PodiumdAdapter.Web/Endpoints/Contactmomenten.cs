using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.ContactmomentenClient.Models;
using Microsoft.AspNetCore.Mvc;
using static Generated.Esuite.ContactmomentenClient.Contactmomenten.ContactmomentenRequestBuilder;
using static Generated.Esuite.ContactmomentenClient.Klantcontactmomenten.KlantcontactmomentenRequestBuilder;
using static Generated.Esuite.ContactmomentenClient.Objectcontactmomenten.ObjectcontactmomentenRequestBuilder;

namespace PodiumdAdapter.Web.Endpoints
{
    public class Contactmomenten
    {
        public static void Api(IEndpointRouteBuilder endpointRouteBuilder)
        {
            var root = endpointRouteBuilder.MapGroup("/contactmomenten/api/v1");
            var contactmomenten = root.MapGroup("/contactmomenten");
            contactmomenten.MapGet("/", Get);
            contactmomenten.MapGet("/{id:guid}", GetById);
            contactmomenten.MapPost("/", Post);

            root.MapGet("/klantcontactmomenten", KlantContactmomenten);
            root.MapGet("/objectcontactmomenten", ObjectContactmomenten);
        }

        public static Task<IResult> Get(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] ContactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Contactmomenten
                .GetAsync(x => x.QueryParameters = query)
                .ToResult(logger);

        public static Task<IResult> Post(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            Contactmoment contactmoment
            ) =>
            client.Contactmomenten
                .PostAsync(contactmoment)
                .ToResult(logger);

        public static Task<IResult> GetById(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [FromRoute] Guid id
            ) =>
            client.Contactmomenten[id]
                .GetAsync()
                .ToResult(logger);

        public static Task<IResult> KlantContactmomenten(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] KlantcontactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Klantcontactmomenten
                .GetAsync(x => x.QueryParameters = query)
                .ToResult(logger);

        public static Task<IResult> ObjectContactmomenten(
            ILogger<Contactmomenten> logger,
            ContactmomentenClient client,
            [AsParameters] ObjectcontactmomentenRequestBuilderGetQueryParameters query
            ) =>
            client.Objectcontactmomenten
                .GetAsync(x => x.QueryParameters = query)
                .ToResult(logger);


    }
}
