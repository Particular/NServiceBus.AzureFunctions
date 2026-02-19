namespace IntegrationTest.Sales.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class AcceptOrderHandler(ILogger<AcceptOrderHandler> logger, MyComponent component) : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        logger.LogWarning($"Handling {nameof(SubmitOrder)} in {nameof(AcceptOrderHandler)}");

        await context.Publish(new OrderSubmitted()).ConfigureAwait(false);
    }
}