using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PodiumdAdapter.Web.Auth
{
    public static class AuthExtensions
    {
        public static void AddAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var authenticationBuilder = services.AddAuthentication();

            authenticationBuilder.AddJwtBearer("zgw", opts =>
            {
                opts.TokenValidationParameters =
                    new TokenValidationParameters()
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeyResolver = (_, token, _, _) => GetKey(configuration, token),
                        ValidateIssuer = true,
                        ValidIssuers = GetCredentials(configuration).Select(x => x.ID),
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        LifetimeValidator = (notBefore, expires, securityToken, validationParameters) =>
                            expires.HasValue && expires - DateTime.Now < TimeSpan.FromHours(1),
                        ClockSkew = TimeSpan.FromMinutes(1),
                    };
            });

            var authorizationBuilder = services.AddAuthorizationBuilder();

            authorizationBuilder.AddFallbackPolicy("zgw", p => p.RequireClaim("client_id").RequireClaim("user_id").RequireClaim("user_representation"));
        }

        public static void RequireObjectenApiKey<T>(this T builder) where T : IEndpointConventionBuilder => builder
            // allow anonymous zorgt ervoor dat de bearer authenticatie uitgeschakeld wordt
            .AllowAnonymous()
            .AddEndpointFilter<T, HasObjectenApiKeyFilter>();

        private static IEnumerable<SecurityKey> GetKey(IConfiguration configuration, SecurityToken token)
        {
            var result = GetCredentials(configuration)
                .Where(x => x.ID == token.Issuer)
                .Select(x => x.SECRET)
                .Select(Encoding.UTF8.GetBytes)
                .Select(x => new SymmetricSecurityKey(x))
                .FirstOrDefault();

            yield return result ?? throw new Exception("Geen security key gevonden");
        }

        private static IEnumerable<ClientCredential> GetCredentials(IConfiguration configuration)
        {
            try
            {
                return configuration?
                    .GetSection("CLIENTS")?
                    .Get<IEnumerable<ClientCredential>>()
                    ?? Enumerable.Empty<ClientCredential>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<ClientCredential>();
            }
        }

        private record ClientCredential(string ID, string SECRET);

        private class HasObjectenApiKeyFilter : IEndpointFilter
        {
            private readonly IConfiguration _configuration;

            public HasObjectenApiKeyFilter(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
            {
                var authHeader = context.HttpContext.Request.Headers.Authorization;

                if (!AuthenticationHeaderValue.TryParse(authHeader, out var header))
                    return new ValueTask<object?>(Results.Problem("Authorization header is missing", statusCode: StatusCodes.Status401Unauthorized));

                if (!GetCredentials(_configuration).Select(x => x.SECRET).Contains(header.Parameter))
                    return new ValueTask<object?>(Results.Problem("Authorization header value is incorrect", statusCode: StatusCodes.Status401Unauthorized));

                return next(context);
            }
        }
    }
}
