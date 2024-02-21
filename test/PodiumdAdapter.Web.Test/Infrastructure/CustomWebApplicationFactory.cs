﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PodiumdAdapter.Web.Infrastructure;
using Yarp.ReverseProxy.Forwarder;

namespace PodiumdAdapter.Web.Test.Infrastructure
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _clientId = Guid.NewGuid().ToString();
        private readonly string _clientSecret = Guid.NewGuid().ToString();

        public readonly MockHttpMessageHandler MockHttpMessageHandler = new();

        public readonly string ESUITE_BASE_URL = "https://localhost:12345";
        public readonly string INTERNE_TAAK_OBJECT_TYPE_URL = "my-type";
        public readonly string AFDELINGEN_BASE_URL = "https://localhost:6789";
        public readonly string GROEPEN_BASE_URL = "https://localhost:5432";
        public readonly string AFDELINGEN_OBJECT_TYPE_URL = "my-afdelingen-type";
        public readonly string GROEPEN_OBJECT_TYPE_URL = "my-groepen-type";
        public readonly string SMOELENBOEK_OBJECT_TYPE_URL = "my-smoelenboek-type";
        public readonly string SMOELENBOEK_BASE_URL = "https://localhost:2222";

        public string LastRequest { get; private set; } = "";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new CustomForwarderHttpClientFactory(MockHttpMessageHandler));
                services.ConfigureHttpClientDefaults(x => x.ConfigurePrimaryHttpMessageHandler(_ => MockHttpMessageHandler));
            });

            var dict = new Dictionary<string, string?>
            {
                ["CLIENTS:0:ID"] = _clientId,
                ["CLIENTS:0:SECRET"] = _clientSecret,
                ["ESUITE_BASE_URL"] = ESUITE_BASE_URL,
                ["ESUITE_CLIENT_ID"] = "FAKE_ID",
                ["ESUITE_CLIENT_SECRET"] = "FAKE_SECRET_OF_AT_LEAST_32_CHARS",
                ["CONTACTVERZOEK_TYPES:0"] = "TYPE",
                ["INTERNE_TAAK_OBJECT_TYPE_URL"] = INTERNE_TAAK_OBJECT_TYPE_URL,
                ["AFDELINGEN_BASE_URL"] = AFDELINGEN_BASE_URL,
                ["GROEPEN_BASE_URL"] = GROEPEN_BASE_URL,
                ["AFDELINGEN_OBJECT_TYPE_URL"] = AFDELINGEN_OBJECT_TYPE_URL,
                ["GROEPEN_OBJECT_TYPE_URL"] = GROEPEN_OBJECT_TYPE_URL,
                ["AFDELINGEN_TOKEN"] = "FAKE_TOKEN",
                ["GROEPEN_TOKEN"] = "FAKE_TOKEN",
                ["SMOELENBOEK_OBJECT_TYPE_URL"] = SMOELENBOEK_OBJECT_TYPE_URL,
                ["SMOELENBOEK_BASE_URL"] = SMOELENBOEK_BASE_URL,
                ["SMOELENBOEK_TOKEN"] = "FAKE_TOKEN",
            };



            builder.ConfigureAppConfiguration((context, configuration) =>
            {
                
                configuration.AddInMemoryCollection(dict);
            });
        }

        public void SetZgwToken(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", GetToken(_clientId, _clientSecret));
        }

        public void SetObjectenToken(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Token", _clientSecret);
        }

        private static string GetToken(string id, string secret)
        {
            return ESuiteClientExtensions.GetToken(id, secret, new Dictionary<string, object>
            {
                { "client_id", id },
                { "user_id", string.Empty},
                { "user_representation", string.Empty }
            });
        }

        private class CustomForwarderHttpClientFactory(MockHttpMessageHandler handler) : IForwarderHttpClientFactory
        {
            public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(handler);
        }

    }
}
