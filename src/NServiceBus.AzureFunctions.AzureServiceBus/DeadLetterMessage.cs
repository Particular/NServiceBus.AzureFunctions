namespace NServiceBus.AzureFunctions.AzureServiceBus;

using Pipeline;
using Transport;

public sealed class DeadLetterMessage : RecoverabilityAction
{
    internal DeadLetterMessage(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null) =>
        deadLetterRequest = new DeadLetterRequest(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

    internal DeadLetterMessage(Exception exception) =>
        deadLetterRequest = new DeadLetterRequest(exception);

    public override IReadOnlyCollection<IRoutingContext> GetRoutingContexts(IRecoverabilityActionContext context)
    {
        context.Extensions.Get<TransportTransaction>().Set(deadLetterRequest);
        return [];
    }

    public override ErrorHandleResult ErrorHandleResult => ErrorHandleResult.Handled;

    readonly DeadLetterRequest deadLetterRequest;
}