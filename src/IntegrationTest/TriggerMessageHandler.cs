namespace IntegrationTest;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NServiceBus;

public class TriggerMessageHandler(ILogger<TriggerMessageHandler> logger) : IHandleMessages<TriggerMessage>
{
    public async Task Handle(TriggerMessage message, IMessageHandlerContext context)
    {
        logger.LogWarning($"Handling {nameof(TriggerMessage)} in {nameof(TriggerMessageHandler)}");

        await context.SendLocal(new SomeOtherMessage()).ConfigureAwait(false);
        await context.Publish(new SomeEvent()).ConfigureAwait(false);
    }
}