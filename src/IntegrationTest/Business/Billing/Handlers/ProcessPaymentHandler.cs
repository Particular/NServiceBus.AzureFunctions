namespace IntegrationTest.Business.Billing.Handlers;

public class ProcessPaymentHandler : IHandleMessages<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted message, IMessageHandlerContext context)
    {
        await context.Publish(new PaymentCleared()).ConfigureAwait(false);
    }
}