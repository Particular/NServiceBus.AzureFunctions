namespace IntegrationTest;

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

public partial class BillingFunctions
{
    [Function("BillingApi")]
    [NServiceBusFunction(typeof(ApiConfig))]
    public partial Task Api(
        [ServiceBusTrigger("billing-api", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public class ApiConfig : IEndpointConfiguration
    {
        public void Configure(EndpointConfiguration configuration)
        {
            CommonConfig.Apply(configuration);

            configuration.AddHandler<TriggerMessageHandler>();
            configuration.AddHandler<SomeOtherMessageHandler>();
        }
    }

    [Function("BillingBackend")]
    [NServiceBusFunction(typeof(BackendConfig))]
    public partial Task Backend(
        [ServiceBusTrigger("billing-backend", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public class BackendConfig : IEndpointConfiguration
    {
        public void Configure(EndpointConfiguration configuration)
        {
            CommonConfig.Apply(configuration);

            configuration.AddHandler<SomeEventMessageHandler>();
            configuration.AddHandler<SomeOtherMessageHandler>();
        }
    }
    public class CommonConfig
    {
        public static void Apply(EndpointConfiguration configuration)
        {
            configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
            configuration.EnableInstallers();
            configuration.UsePersistence<LearningPersistence>();
            configuration.UseSerialization<SystemJsonSerializer>();
        }
    }
}