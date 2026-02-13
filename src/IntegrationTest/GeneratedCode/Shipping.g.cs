namespace IntegrationTest;

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

public partial class Shipping
{
    public partial Task Invoke(
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default)
    {
        //demo using service locator to avoid having to add a ctor
        var processor = functionContext.InstanceServices.GetKeyedService<IMessageProcessor>(EndpointName);

        if (processor is null)
        {
            //which also allows us to throw a better exception
            throw new InvalidOperationException($"{EndpointName} has not been registered.");
        }

        return processor.Process(message, cancellationToken);
    }
    
    
    const string EndpointName = "Shipping";
}