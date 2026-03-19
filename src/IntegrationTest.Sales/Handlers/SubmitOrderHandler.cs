namespace IntegrationTest.Sales.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class SubmitOrderHandler(ILogger<SubmitOrderHandler> logger, MyComponent component) : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        logger.LogWarning($"Handling {nameof(SubmitOrder)} in {nameof(SubmitOrderHandler)} with component for {component.EndpointName}");

        await context.Publish(new OrderSubmitted()).ConfigureAwait(false);
    }
}