namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;
using NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;
using NServiceBus.Transport;

public class AzureServiceBusServerlessTransport : ServerlessTransport
{
    readonly AzureServiceBusTransport innerTransport;
    readonly string? connectionString;
    readonly string connectionName;

    public AzureServiceBusServerlessTransport(TopicTopology topology, string? connection = null)
        : base(TransportTransactionMode.ReceiveOnly,
               supportsDelayedDelivery: true,
               supportsPublishSubscribe: true,
               supportsTtbr: true)
    {
        innerTransport = new AzureServiceBusTransport("TransportWillBeInitializedCorrectlyLater", topology)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly
        };

        if (connection != null && connection.Contains(';'))
        {
            connectionString = connection;
            connectionName = DefaultServiceBusConnectionName;
        }
        else
        {
            connectionString = null;
            connectionName = connection ?? DefaultServiceBusConnectionName;
        }
    }

    internal IInternalMessageProcessor MessageProcessor { get; private set; } = null!;

    public override async Task<TransportInfrastructure> Initialize(
        HostSettings hostSettings,
        ReceiveSettings[] receivers,
        string[] sendingAddresses,
        CancellationToken cancellationToken = default)
    {
        var configuredTransport = ConfigureTransportConnection(
            connectionString,
            connectionName,
            ServiceProvider!.GetRequiredService<IConfiguration>(),
            innerTransport,
            ServiceProvider.GetRequiredService<AzureComponentFactory>());

        var baseTransportInfrastructure = await configuredTransport.Initialize(
                hostSettings,
                receivers,
                sendingAddresses,
                cancellationToken)
            .ConfigureAwait(false);

        var serverlessTransportInfrastructure = new ServerlessTransportInfrastructure(baseTransportInfrastructure);

        var isSendOnly = hostSettings.CoreSettings.GetOrDefault<bool>(SendOnlyConfigKey);

        MessageProcessor = isSendOnly
            ? new SendOnlyMessageProcessor()
            : (IInternalMessageProcessor)serverlessTransportInfrastructure.Receivers[MainReceiverId];

        return serverlessTransportInfrastructure;
    }

    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => supportedTransactionModes;

    public override void RegisterServices(IServiceCollection services, string endpointName)
    {
        services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
            new MessageProcessor(this, sp.GetRequiredKeyedService<NServiceBus.AzureFunctions.EndpointStarter>(endpointName)));
    }

    static AzureServiceBusTransport ConfigureTransportConnection(
        string? connectionString,
        string connectionName,
        IConfiguration configuration,
        AzureServiceBusTransport transport,
        AzureComponentFactory azureComponentFactory)
    {
        if (connectionString != null)
        {
            GetConnectionStringRef(transport) = connectionString;
        }
        else
        {
            var serviceBusConnectionName = string.IsNullOrWhiteSpace(connectionName) ? DefaultServiceBusConnectionName : connectionName;
            IConfigurationSection connectionSection = configuration.GetSection(serviceBusConnectionName);
            if (!connectionSection.Exists())
            {
                throw new Exception($"Azure Service Bus connection string/section has not been configured. Specify a connection string through IConfiguration, an environment variable named {serviceBusConnectionName} or passing it to the transport constructor.");
            }

            if (!string.IsNullOrWhiteSpace(connectionSection.Value))
            {
                GetConnectionStringRef(transport) = connectionSection.Value;
            }
            else
            {
                string? fullyQualifiedNamespace = connectionSection["fullyQualifiedNamespace"];
                if (string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
                {
                    throw new Exception("Connection should have an 'fullyQualifiedNamespace' property or be a string representing a connection string.");
                }

                var credential = azureComponentFactory.CreateTokenCredential(connectionSection);
                GetFullyQualifiedNamespaceRef(transport) = fullyQualifiedNamespace;
                GetTokenCredentialRef(transport) = credential;
            }
        }

        return transport;
    }

    // As a temporary workaround we are accessing the properties of the AzureServiceBusTransport using UnsafeAccessor
    // This is another blocker to AoT but we are already using the execution assembly in the code base anyway
    // Furthermore this allows us to still comply with initializing the transport as late as possible without having to
    // expose the properties on the transport itself which would pollute the public API for not much added value.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<ConnectionString>k__BackingField")]
    static extern ref string GetConnectionStringRef(AzureServiceBusTransport transport);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<FullyQualifiedNamespace>k__BackingField")]
    static extern ref string GetFullyQualifiedNamespaceRef(AzureServiceBusTransport transport);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<TokenCredential>k__BackingField")]
    static extern ref TokenCredential GetTokenCredentialRef(AzureServiceBusTransport transport);

    const string MainReceiverId = "Main";
    const string SendOnlyConfigKey = "Endpoint.SendOnly";
    internal const string DefaultServiceBusConnectionName = "AzureWebJobsServiceBus";

    readonly TransportTransactionMode[] supportedTransactionModes = [TransportTransactionMode.ReceiveOnly];
}