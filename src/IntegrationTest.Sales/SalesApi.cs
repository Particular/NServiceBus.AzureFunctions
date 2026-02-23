namespace IntegrationTest.Sales;

using System.Net;
using System.Threading.Tasks;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;

class SalesApi(
    [FromKeyedServices("client")] IMessageSession session,
    [FromKeyedServices("client")] MyComponent component,
    MyComponent globalComponent,
    ILogger<SalesApi> logger)
{
    [Function("SalesApi")]
    public async Task<HttpResponseData> Api(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"Sales HTTP api triggered. Injected component from: {component.EndpointName} and {globalComponent.EndpointName}");

        await session.Send(new SubmitOrder(), cancellationToken).ConfigureAwait(false);

        var r = req.CreateResponse(HttpStatusCode.OK);
        await r.WriteStringAsync($"{nameof(SubmitOrder)} sent.", cancellationToken).ConfigureAwait(false);
        return r;
    }
}