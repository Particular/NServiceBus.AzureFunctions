namespace IntegrationTest;

using Azure.Messaging.ServiceBus;
using ITOps;
using Microsoft.Azure.Functions.Worker;

public partial class Shipping : IEndpointConfiguration
{
    [Function("Shipping")]
    [NServiceBusFunction(typeof(Shipping))] //we could default to containing type?
    public partial Task Invoke(
        [ServiceBusTrigger("shipping", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);


    public void Configure(EndpointConfiguration configuration)
    {
        CommonConfig.Apply(configuration);

        configuration.AddHandler<SomeEventMessageHandler>();
    }
}