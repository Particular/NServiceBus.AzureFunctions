namespace IntegrationTest;

using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

public partial class SomeEventMessageHandler : IHandleMessages<SomeEvent>
{
    // using static logger here deliberately
    static readonly ILog Log = LogManager.GetLogger<SomeOtherMessageHandler>();

    public Task Handle(SomeEvent message, IMessageHandlerContext context)
    {
        Log.Warn($"Handling {nameof(SomeEvent)} in {nameof(SomeEventMessageHandler)}");

        return Task.CompletedTask;
    }
}