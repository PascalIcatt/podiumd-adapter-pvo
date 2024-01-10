namespace PodiumdAdapter.Web.Infrastructure
{
    public delegate void Endpoint(IEndpointRouteBuilder builder);

    public static class MapEndpointExtensions
    {
        public static void Map(this IEndpointRouteBuilder builder, Endpoint endpoint) => endpoint(builder);
    }
}
