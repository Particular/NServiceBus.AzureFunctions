namespace IntegrationTest.Sales.Handlers;

using IntegrationTest.Shared;
using NServiceBus;
using NServiceBus.Logging;

public class AcceptOrderHandler : IHandleMessages<SubmitOrder>
{
    static readonly ILog Log = LogManager.GetLogger<AcceptOrderHandler>();

    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        Log.Warn($"Handling {nameof(SubmitOrder)} in {nameof(AcceptOrderHandler)}");

        await context.Publish(new OrderSubmitted()).ConfigureAwait(false);
    }
}