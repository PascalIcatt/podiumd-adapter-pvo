using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.KlantenClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Moq;

namespace PodiumdAdapter.Web.Test.Infrastructure
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _clientId = Guid.NewGuid().ToString();
        private readonly string _clientSecret = Guid.NewGuid().ToString();
        private readonly Mock<IRequestAdapter> _requestAdapter = GetRequestAdapter();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_requestAdapter.Object);
                services.AddSingleton<ContactmomentenClient>();
                services.AddSingleton<KlantenClient>();
            });

            builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CLIENTS:0:ID"] = _clientId,
                ["CLIENTS:0:SECRET"] = _clientSecret,
            }));
        }

        public void Login(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", GetToken(_clientId, _clientSecret));
        }

        public void SetEsuiteResponse<T>(T value) where T : IParsable
        {
            _requestAdapter.Setup(x => x.SendAsync(
                It.IsAny<RequestInformation>(),
                It.IsAny<ParsableFactory<T>>(),
                It.IsAny<Dictionary<string, ParsableFactory<IParsable>>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(value);
        }

        public void SetEsuiteError<TModel>(Exception exception) where TModel : IParsable
        {
            _requestAdapter.Setup(x => x.SendAsync(
                It.IsAny<RequestInformation>(),
                It.IsAny<ParsableFactory<TModel>>(),
                It.IsAny<Dictionary<string, ParsableFactory<IParsable>>>(),
                It.IsAny<CancellationToken>()))
                .Throws(exception);
        }

        private static string GetToken(string id, string secret)
        {
            var secretKey = secret; // "een sleutel van minimaal 16 karakters";
            var client_id = id;
            var iss = id;
            var user_id = string.Empty;
            var user_representation = string.Empty;
            var now = DateTimeOffset.UtcNow;
            // one minute leeway to account for clock differences between machines
            var issuedAt = now.AddMinutes(-1);
            var iat = issuedAt.ToUnixTimeSeconds();

            var claims = new Dictionary<string, object>
                {
                    { "client_id", client_id },
                    { "iss", iss },
                    { "iat", iat },
                    { "user_id", user_id},
                    { "user_representation", user_representation }
                };

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                IssuedAt = issuedAt.DateTime,
                NotBefore = issuedAt.DateTime,
                Claims = claims,
                Subject = new ClaimsIdentity(),
                Expires = now.AddHours(1).DateTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static Mock<IRequestAdapter> GetRequestAdapter()
        {
            var factoryMock = new Mock<ISerializationWriterFactory>();
            factoryMock.Setup(x => x.GetSerializationWriter(It.IsAny<string>())).Returns(() => new JsonSerializationWriter());
            var requestAdapter = new Mock<IRequestAdapter>();
            requestAdapter.Setup(x => x.SerializationWriterFactory).Returns(factoryMock.Object);
            return requestAdapter;
        }
    }
}
