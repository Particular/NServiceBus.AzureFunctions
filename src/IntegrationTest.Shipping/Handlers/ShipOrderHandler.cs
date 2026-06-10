using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1050
[Handler]
public class ShipOrderHandler(ILogger<ShipOrderHandler> logger) : IHandleMessages<PaymentCleared>
#pragma warning restore CA1050
{
    public Task Handle(PaymentCleared message, IMessageHandlerContext context)
    {
        logger.LogWarning("Handling {MessageType} in {HandlerType}", nameof(PaymentCleared), nameof(ShipOrderHandler));

        return Task.CompletedTask;
    }
}