namespace IntegrationTest.Billing.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

[Handler]
public class ProcessPaymentHandler(ILogger<ProcessPaymentHandler> logger) : IHandleMessages<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted message, IMessageHandlerContext context)
    {
        logger.LogWarning("Handling {MessageType} in {HandlerType}", nameof(OrderSubmitted), nameof(ProcessPaymentHandler));

        await context.Publish(new PaymentCleared());
    }
}