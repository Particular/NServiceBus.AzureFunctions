namespace NServiceBus.AzureFunctions.AzureServiceBus;

using Pipeline;
using Transport;

public class DeadLetterMessage : RecoverabilityAction
{
    public DeadLetterMessage(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null) =>
        deadLetterRequest = new DeadLetterRequest(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

    public DeadLetterMessage(Exception exception) =>
        deadLetterRequest = new DeadLetterRequest(exception);

    public override IReadOnlyCollection<IRoutingContext> GetRoutingContexts(IRecoverabilityActionContext context)
    {
        context.Extensions.Get<TransportTransaction>().Set(deadLetterRequest);
        return [];
    }

    public override ErrorHandleResult ErrorHandleResult => ErrorHandleResult.Handled;

    readonly DeadLetterRequest deadLetterRequest;
}