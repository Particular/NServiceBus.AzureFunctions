using IntegrationTest;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus.Logging;
using NServiceBus.Logging;
using HostingAbstractionsHostExtensions = Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions;

var builder = FunctionsApplication.CreateBuilder(args);

// as early as possible
LogManager.UseFactory(FunctionsLoggerFactory.Instance);

builder.Services.AddHostedService<InitializeLogger>();

builder.Sender();
builder.Receiver();
builder.AnotherReceiver();

var host = builder.Build();



//// single 
//builder.UseNServiceBus()
//    .AddEndpoint("SampleEndpoint", config =>
//    {
//        config.UseSerialization<SystemJsonSerializer>();
//    });

////multi
// builder.UseNServiceBus()
//     .AddEndpoint("Shipping", config =>
//     {
//         config.UseSerialization<SystemJsonSerializer>();
//     })
//     .AddEndpoint("Billing", config =>
//     {
//         config.UseSerialization<SystemJsonSerializer>();
//     });



//var host = builder.Build();

await HostingAbstractionsHostExtensions.RunAsync(host).ConfigureAwait(false);

namespace IntegrationTest
{
    using Microsoft.Azure.Functions.Worker.Builder;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AzureFunctions.AzureServiceBus;
    using NServiceBus.AzureFunctions.AzureServiceBus.Logging;
    using NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;
    using NServiceBus.MultiHosting;
    using NServiceBus.MultiHosting.Services;

    public static class SenderEndpointConfigurationExtensions
    {
        public static void Sender(this FunctionsApplicationBuilder builder)
        {
            builder.Services.AddAzureClientsCore();

            var serviceKey = "SenderEndpoint";

            using var _ = FunctionsLoggerFactory.Instance.PushName(serviceKey);

            var endpointConfiguration = new EndpointConfiguration(serviceKey);
            endpointConfiguration.SendOnly();
            //endpointConfiguration.EnableOutbox();

            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.Disable = true;

            var persistence = endpointConfiguration.UsePersistence<LearningPersistence>();
            //persistence.DatabaseName("SharedBetweenSenderAndReceiver");
            //persistence.EnableTransactionalSession(new TransactionalSessionOptions
            //{
            //    ProcessorEndpoint = "ReceiverEndpoint"
            //});
            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            var transport = new AzureServiceBusTransport("TransportWillBeInitializedCorrectlyLater", TopicTopology.Default)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            };
            var serverlessTransport = new ServerlessTransport(transport, null, "AzureWebJobsServiceBus");
            endpointConfiguration.UseTransport(serverlessTransport);

            var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, serviceKey);
            var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
                endpointConfiguration,
                keyedServices);

            builder.Services.AddKeyedSingleton(serviceKey, (sp, __) => new EndpointStarter(startableEndpoint, sp, serverlessTransport, serviceKey, keyedServices));
            // unfortunately AddHostedServices dedups
            builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(s => new NServiceBusHostedService(s.GetRequiredKeyedService<EndpointStarter>(serviceKey)));
            builder.Services.AddKeyedSingleton<IMessageSession>(serviceKey, (sp, key) => new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));

            //builder.UseMiddleware<TransactionalSessionMiddleware>();
        }
    }

    public static class ReceiverEndpointConfigurationExtensions
    {
        public static void Receiver(this FunctionsApplicationBuilder builder)
        {
            builder.Services.AddAzureClientsCore();

            var serviceKey = "ReceiverEndpoint";

            using var _ = FunctionsLoggerFactory.Instance.PushName(serviceKey);

            var endpointConfiguration = new EndpointConfiguration(serviceKey);
            //endpointConfiguration.EnableOutbox();

            endpointConfiguration.EnableInstallers();

            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.Disable = true;

            var persistence = endpointConfiguration.UsePersistence<LearningPersistence>();
            //persistence.DatabaseName("SharedBetweenSenderAndReceiver");
            //persistence.EnableTransactionalSession();

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            // hardcoded handlers
            endpointConfiguration.AddHandler<TriggerMessageHandler>();
            endpointConfiguration.AddHandler<SomeOtherMessageHandler>();
            endpointConfiguration.AddHandler<SomeEventMessageHandler>();

            var transport = new AzureServiceBusTransport("TransportWillBeInitializedCorrectlyLater", TopicTopology.Default)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            };
            var serverlessTransport = new ServerlessTransport(transport, null, "AzureWebJobsServiceBus");
            endpointConfiguration.UseTransport(serverlessTransport);

            var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, serviceKey);
            var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
                endpointConfiguration,
                keyedServices);

            builder.Services.AddKeyedSingleton(serviceKey, (sp, __) => new EndpointStarter(startableEndpoint, sp, serverlessTransport, serviceKey, keyedServices));
            // unfortunately AddHostedServices dedups
            builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(sp => new NServiceBusHostedService(sp.GetRequiredKeyedService<EndpointStarter>(serviceKey)));
            builder.Services.AddKeyedSingleton<IMessageSession>(serviceKey, (sp, key) => new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));
            builder.Services.AddKeyedSingleton<IMessageProcessor>(serviceKey, (sp, key) => new MessageProcessor(serverlessTransport, sp.GetRequiredKeyedService<EndpointStarter>(key)));
        }
    }

    public static class AnotherReceiverEndpointConfigurationExtensions
    {
        public static void AnotherReceiver(this FunctionsApplicationBuilder builder)
        {
            builder.Services.AddAzureClientsCore();

            var serviceKey = "AnotherReceiverEndpoint";

            using var _ = FunctionsLoggerFactory.Instance.PushName(serviceKey);

            var endpointConfiguration = new EndpointConfiguration(serviceKey);
            //endpointConfiguration.EnableOutbox();

            endpointConfiguration.EnableInstallers();

            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.Disable = true;

            var persistence = endpointConfiguration.UsePersistence<LearningPersistence>();
            //persistence.EnableTransactionalSession();

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            // hardcoded handlers
            endpointConfiguration.AddHandler<SomeEventMessageHandler>();

            var transport = new AzureServiceBusTransport("TransportWillBeInitializedCorrectlyLater", TopicTopology.Default)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            };
            var serverlessTransport = new ServerlessTransport(transport, null, "AzureWebJobsServiceBus");
            endpointConfiguration.UseTransport(serverlessTransport);

            var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, serviceKey);
            var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
                endpointConfiguration,
                keyedServices);

            builder.Services.AddKeyedSingleton(serviceKey, (sp, __) => new EndpointStarter(startableEndpoint, sp, serverlessTransport, serviceKey, keyedServices));
            // unfortunately AddHostedServices dedups
            builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(sp => new NServiceBusHostedService(sp.GetRequiredKeyedService<EndpointStarter>(serviceKey)));
            builder.Services.AddKeyedSingleton<IMessageSession>(serviceKey, (sp, key) => new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));
            builder.Services.AddKeyedSingleton<IMessageProcessor>(serviceKey, (sp, key) => new MessageProcessor(serverlessTransport, sp.GetRequiredKeyedService<EndpointStarter>(key)));
        }
    }
}