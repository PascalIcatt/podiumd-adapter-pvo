using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PodiumdAdapter.Web.Test
{
    public class SmoelenboekTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task Volledige_naam_is_mapped_to_achternaam()
        {
            const string ObjectenResponse = """"
            {
                "results": [{
                    "record": 
                    {
                        "data": 
                        {
                            "volledigeNaam": "Voor tussen Achter"
                        }
                    }
                }]
            }
            """";
            factory.MockHttpMessageHandler
                .When("/*")
                .Respond("application/json", ObjectenResponse);

            using var client = factory.CreateClient();
            factory.SetObjectenToken(client);
            using var stream = await client.GetStreamAsync("/api/v2/objects?type=" + factory.SMOELENBOEK_OBJECT_TYPE_URL);

            await VerifyJson(stream);
        }
    }
}
