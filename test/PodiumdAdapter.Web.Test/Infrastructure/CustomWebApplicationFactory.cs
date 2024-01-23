﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Forwarder;

namespace PodiumdAdapter.Web.Test.Infrastructure
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _clientId = Guid.NewGuid().ToString();
        private readonly string _clientSecret = Guid.NewGuid().ToString();

        public readonly MockHttpMessageHandler MockHttpMessageHandler = new();

        public readonly string ESUITE_BASE_URL = "https://localhost:12345";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new CustomForwarderHttpClientFactory(MockHttpMessageHandler));
            });

            builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CLIENTS:0:ID"] = _clientId,
                ["CLIENTS:0:SECRET"] = _clientSecret,
                ["ESUITE_BASE_URL"] = ESUITE_BASE_URL,
                ["ESUITE_TOKEN"] = "FAKE_TOKEN",
            }));
        }

        public void Login(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", GetToken(_clientId, _clientSecret));
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

        private class CustomForwarderHttpClientFactory(MockHttpMessageHandler handler) : IForwarderHttpClientFactory
        {
            public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(handler);
        }
    }
}
