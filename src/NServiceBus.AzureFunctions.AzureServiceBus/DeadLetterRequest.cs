namespace NServiceBus.AzureFunctions.AzureServiceBus;

class DeadLetterRequest
{
    public string DeadLetterReason { get; }
    public string DeadLetterErrorDescription { get; }
    public Dictionary<string, object>? PropertiesToModify { get; }

    public DeadLetterRequest(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null)
    {
        DeadLetterReason = Truncate(deadLetterReason, 1024);
        DeadLetterErrorDescription = Truncate(deadLetterErrorDescription, 1024);
        PropertiesToModify = propertiesToModify;
    }

    public DeadLetterRequest(Exception exception) : this(
        $"{exception.GetType().FullName} - {exception.Message}",
        exception.StackTrace ?? "No stack trace available")
    {
    }

    static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value)
            ? value
            : value.Length <= maxLength
                ? value
                : value[..maxLength];
}