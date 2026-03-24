namespace NServiceBus.AzureFunctions.AzureServiceBus;

using Pipeline;
using Transport;

public class DeadLetterMessage(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null) : RecoverabilityAction
{
    public DeadLetterMessage(Exception exception) : this($"{exception.GetType().FullName!} - {exception.Message}", exception.StackTrace ?? exception.ToString(), null)
    {
    }

    public override IReadOnlyCollection<IRoutingContext> GetRoutingContexts(IRecoverabilityActionContext context)
    {
        context.Extensions.Get<TransportTransaction>().Set(new DeadLetterRequest(deadLetterReason, deadLetterErrorDescription, propertiesToModify));
        return [];
    }

    public override ErrorHandleResult ErrorHandleResult => ErrorHandleResult.Handled;
}