namespace IntegrationTest.Shipping.Handlers;

using IntegrationTest.Shared;
using NServiceBus;
using NServiceBus.Logging;

public class ShipOrderHandler : IHandleMessages<PaymentCleared>
{
    static readonly ILog Log = LogManager.GetLogger<ShipOrderHandler>();

    public Task Handle(PaymentCleared message, IMessageHandlerContext context)
    {
        Log.Warn($"Handling {nameof(PaymentCleared)} in {nameof(ShipOrderHandler)}");

        return Task.CompletedTask;
    }
}