namespace IntegrationTest;

using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;

class HttpSender([FromKeyedServices("SenderEndpoint")] IMessageSession session, ILogger<HttpSender> logger)
{
    [Function("HttpSenderV4")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
        FunctionContext executionContext)
    {
        _ = executionContext; // For now
        logger.LogInformation("C# HTTP trigger function received a request.");

        await session.Send(new TriggerMessage()).ConfigureAwait(false);

        var r = req.CreateResponse(HttpStatusCode.OK);
        await r.WriteStringAsync($"{nameof(TriggerMessage)} sent.")
            .ConfigureAwait(false);
        return r;
    }
}