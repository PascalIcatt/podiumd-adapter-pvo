using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PodiumdAdapter.Web.Auth
{
    public static class AuthExtensions
    {
        public static void AddAuth(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication().AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters =
                    new TokenValidationParameters()
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeyResolver = (_, token, _, _) => GetKey(configuration, token),
                        ValidateIssuer = true,
                        ValidIssuers = configuration.GetSection("Clients").Get<ClientCredential[]>()!.Select(x => x.ClientId),
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                    };
            });
            services.AddAuthorizationBuilder().AddFallbackPolicy("zgw", p => p.RequireClaim("client_id").RequireClaim("user_id").RequireClaim("user_representation"));
        }

        private static IEnumerable<SecurityKey> GetKey(IConfiguration configuration, SecurityToken token)
        {
            var result = configuration
                .GetSection("Clients")
                .Get<IEnumerable<ClientCredential>>()?
                .Where(x => x.ClientId == token.Issuer)
                .Select(x => x.ClientSecret)
                .Select(Encoding.UTF8.GetBytes)
                .Select(x => new SymmetricSecurityKey(x))
                .FirstOrDefault();

            yield return result ?? throw new Exception();
        }

        private record ClientCredential(string ClientId, string ClientSecret);
    }
}
