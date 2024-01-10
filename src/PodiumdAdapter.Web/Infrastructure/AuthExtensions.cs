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
                        ValidIssuers = configuration.GetSection("CLIENTS").Get<ClientCredential[]>()!.Select(x => x.ID),
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
                .GetSection("CLIENTS")
                .Get<IEnumerable<ClientCredential>>()?
                .Where(x => x.ID == token.Issuer)
                .Select(x => x.SECRET)
                .Select(Encoding.UTF8.GetBytes)
                .Select(x => new SymmetricSecurityKey(x))
                .FirstOrDefault();

            yield return result ?? throw new Exception();
        }

        private record ClientCredential(string ID, string SECRET);
    }
}
