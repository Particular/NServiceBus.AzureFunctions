namespace IntegrationTest.Billing.Handlers;

using IntegrationTest.Shared;
using NServiceBus;
using NServiceBus.Logging;

public class ProcessPaymentHandler : IHandleMessages<OrderSubmitted>
{
    static readonly ILog Log = LogManager.GetLogger<ProcessPaymentHandler>();

    public async Task Handle(OrderSubmitted message, IMessageHandlerContext context)
    {
        Log.Warn($"Handling {nameof(OrderSubmitted)} in {nameof(ProcessPaymentHandler)}");

        await context.Publish(new PaymentCleared()).ConfigureAwait(false);
    }
}