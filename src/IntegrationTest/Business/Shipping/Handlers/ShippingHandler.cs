namespace IntegrationTest.Business.Shipping.Handlers;

public class ShippingHandler : IHandleMessages<PaymentCleared>
{
    public Task Handle(PaymentCleared message, IMessageHandlerContext context)
    {
        // Order shipped!
        return Task.CompletedTask;
    }
}
