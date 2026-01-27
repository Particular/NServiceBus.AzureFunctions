//namespace NServiceBus;

//using Microsoft.Azure.Functions.Worker.Builder;
//using Microsoft.Extensions.Azure;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using NServiceBus.AzureFunctions.AzureServiceBus;
//using NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;
//using NServiceBus.MultiHosting;
//using NServiceBus.MultiHosting.Services;

///// <summary>
///// Fluent builder for configuring NServiceBus endpoints in Azure Functions multi-hosting scenarios.
///// </summary>
//public class NServiceBusBuilder
//{
//    readonly FunctionsApplicationBuilder builder;

//    internal NServiceBusBuilder(FunctionsApplicationBuilder builder)
//    {
//        this.builder = builder;
//    }
//    public NServiceBusBuilder AddEndpoint(
//        string endpointName,
//        Action<EndpointConfiguration> configure,
//        string connectionStringOrName = ServerlessTransport.DefaultServiceBusConnectionName)
//    {
//        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

//        builder.Services.AddAzureClientsCore();

//        var endpointConfiguration = new EndpointConfiguration(endpointName);

//        var assemblyScanner = endpointConfiguration.AssemblyScanner();
//        assemblyScanner.Disable = true;

//        configure?.Invoke(endpointConfiguration);

//        var transport = new AzureServiceBusTransport("TransportWillBeInitializedCorrectlyLater", TopicTopology.Default)
//        {
//            TransportTransactionMode = TransportTransactionMode.ReceiveOnly
//        };
//        var serverlessTransport = new ServerlessTransport(transport, null, connectionStringOrName);
//        endpointConfiguration.UseTransport(serverlessTransport);

//        var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, endpointName);
//        var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
//            endpointConfiguration,
//            keyedServices);

//        builder.Services.AddKeyedSingleton<IEndpointStarter>(endpointName, (sp, _) =>
//            new EndpointStarter(startableEndpoint, sp, serverlessTransport, endpointName, keyedServices));

//        builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(sp =>
//            new NServiceBusHostedService(sp.GetRequiredKeyedService<IEndpointStarter>(endpointName)));

//        builder.Services.AddKeyedSingleton<IMessageSession>(endpointName, (sp, key) =>
//            new HostAwareMessageSession(sp.GetRequiredKeyedService<IEndpointStarter>(key)));

//        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
//            new MessageProcessor(serverlessTransport, sp.GetRequiredKeyedService<IEndpointStarter>(endpointName)));

//        return this;
//    }
//}
