namespace IntegrationTest.Sales;

using System.Net;
using System.Threading.Tasks;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;

[NServiceBusSendOnlyEndpoint(configurationType: typeof(Config))]
class SalesApi([FromKeyedServices("SalesApi")] IMessageSession session, ILogger<SalesApi> logger)
{
    [Function("SalesApi")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("C# HTTP trigger function received a request.");

        await session.Send(new SubmitOrder(), cancellationToken).ConfigureAwait(false);

        var r = req.CreateResponse(HttpStatusCode.OK);
        await r.WriteStringAsync($"{nameof(SubmitOrder)} sent.", cancellationToken).ConfigureAwait(false);
        return r;
    }

    public class Config : IEndpointConfiguration
    {
        public void Configure(EndpointConfiguration configuration)
        {
            var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default)
            {
                ConnectionName = "AzureWebJobsServiceBus"
            };

            var routing = configuration.UseTransport(transport);

            routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
            configuration.UseSerialization<SystemJsonSerializer>();
        }
    }
}