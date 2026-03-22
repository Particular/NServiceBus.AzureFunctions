namespace NServiceBus.AzureFunctions.AzureServiceBus;

record DeadLetterRequest(string DeadLetterReason, string DeadLetterErrorDescription, Dictionary<string, object>? PropertiesToModify);