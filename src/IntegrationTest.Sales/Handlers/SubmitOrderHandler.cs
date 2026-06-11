namespace IntegrationTest.Sales.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

[Handler]
public class SubmitOrderHandler(ILogger<SubmitOrderHandler> logger, MyComponent component) : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        logger.LogWarning("Handling {MessageType} in {HandlerType} with component for {EndpointName}", nameof(SubmitOrder), nameof(SubmitOrderHandler), component.EndpointName);

        await context.Publish(new OrderSubmitted()).ConfigureAwait(false);
    }
}