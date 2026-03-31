namespace NServiceBus.AzureFunctions.AzureServiceBus;

public static class RecoverabilityActionExtensions
{
    extension(RecoverabilityAction _)
    {
        public static DeadLetterMessage DeadLetter(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null)
            => new(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

        public static DeadLetterMessage DeadLetter(Exception exception) => new(exception);
    }
}