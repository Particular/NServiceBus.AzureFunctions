namespace IntegrationTest.Shipping.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class ShipOrderHandler(ILogger<ShipOrderHandler> logger) : IHandleMessages<PaymentCleared>
{
    public Task Handle(PaymentCleared message, IMessageHandlerContext context)
    {
        logger.LogWarning($"Handling {nameof(PaymentCleared)} in {nameof(ShipOrderHandler)}");

        return Task.CompletedTask;
    }
}