namespace IntegrationTest;

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

public partial class BillingFunctions
{
    public partial Task Api(
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default)
    {
        //demo using service locator to avoid having to add a ctor
        var processor = functionContext.InstanceServices.GetKeyedService<IMessageProcessor>(ApiEndpointName);

        if (processor is null)
        {
            //which also allows us to throw a better exception
            throw new InvalidOperationException($"{ApiEndpointName} has not been registered.");
        }

        return processor.Process(message, cancellationToken);
    }
    
    public partial Task Backend(
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default)
    {
        //demo using service locator to avoid having to add a ctor
        var processor = functionContext.InstanceServices.GetKeyedService<IMessageProcessor>(BackendEndpointName);

        if (processor is null)
        {
            //which also allows us to throw a better exception
            throw new InvalidOperationException($"{BackendEndpointName} has not been registered.");
        }

        return processor.Process(message, cancellationToken);
    }
    
    const string ApiEndpointName = "billing-api";
    const string BackendEndpointName = "billing-backend";
}