namespace IntegrationTest.Sales.Handlers;

using IntegrationTest.Shared;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class SubmitOrderHandler(ILogger<SubmitOrderHandler> logger, MyComponent component) : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        throw new NotImplementedException("dsxc");
    }
}