namespace IntegrationTest.Business.Sales.Handlers;

public class AcceptOrderHandler : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        await context.Publish(new OrderSubmitted()).ConfigureAwait(false);
    }
}
