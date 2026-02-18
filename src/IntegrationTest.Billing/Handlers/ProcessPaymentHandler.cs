namespace IntegrationTest.Billing.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class ProcessPaymentHandler(ILogger<ProcessPaymentHandler> logger) : IHandleMessages<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted message, IMessageHandlerContext context)
    {
        logger.LogWarning($"Handling {nameof(OrderSubmitted)} in {nameof(ProcessPaymentHandler)}");

        await context.Publish(new PaymentCleared()).ConfigureAwait(false);
    }
}