using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;

public class ShipOrderHandler(ILogger<ShipOrderHandler> logger) : IHandleMessages<PaymentCleared>
{
    public Task Handle(PaymentCleared message, IMessageHandlerContext context)
    {
        logger.LogWarning("Handling {MessageType} in {HandlerType}", nameof(PaymentCleared), nameof(ShipOrderHandler));

        return Task.CompletedTask;
    }
}