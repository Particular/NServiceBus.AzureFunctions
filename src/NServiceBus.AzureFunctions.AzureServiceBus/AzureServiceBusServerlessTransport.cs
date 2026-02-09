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
using NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;
using NServiceBus.Transport;
using Settings;

public class AzureServiceBusServerlessTransport(TopicTopology topology) : TransportDefinition(TransportTransactionMode.ReceiveOnly,
    supportsDelayedDelivery: true,
    supportsPublishSubscribe: true,
    supportsTTBR: true)
{
    public string ConnectionName { get; set; } = DefaultServiceBusConnectionName;

    internal IInternalMessageProcessor MessageProcessor { get; private set; } = null!;

//    public RoutingSettings<AzureServiceBusTransport> Routing => new(Settings);
//
//    internal SettingsHolder Settings { get; } = new SettingsHolder();

    protected override void ConfigureServicesCore(IServiceCollection services) => innerTransport.ConfigureServices(services);

    public override async Task<TransportInfrastructure> Initialize(
        HostSettings hostSettings,
        ReceiveSettings[] receivers,
        string[] sendingAddresses,
        CancellationToken cancellationToken = default)
    {
        if (!hostSettings.SupportsDependencyInjection)
        {
            throw new Exception("Dependency injection is required.");
        }

        if (hostSettings.IsRawMode)
        {
            throw new Exception("Raw mode is not supported.");
        }

        ConfigureTransportConnection(
            ConnectionName,
            hostSettings.ServiceProvider.GetRequiredService<IConfiguration>(),
            hostSettings.ServiceProvider.GetRequiredService<AzureComponentFactory>());

        var baseTransportInfrastructure = await innerTransport.Initialize(
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

    void ConfigureTransportConnection(
        string connectionName,
        IConfiguration configuration,
        AzureComponentFactory azureComponentFactory)
    {
        var resolvedName = string.IsNullOrWhiteSpace(connectionName) ? DefaultServiceBusConnectionName : connectionName;
        IConfigurationSection connectionSection = configuration.GetSection(resolvedName);
        if (!connectionSection.Exists())
        {
            throw new Exception($"Azure Service Bus connection has not been configured. Add a configuration section named '{resolvedName}' with a connection string or fullyQualifiedNamespace.");
        }

        if (!string.IsNullOrWhiteSpace(connectionSection.Value))
        {
            GetConnectionStringRef(innerTransport) = connectionSection.Value;
        }
        else
        {
            string? fullyQualifiedNamespace = connectionSection["fullyQualifiedNamespace"];
            if (string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
            {
                throw new Exception($"Configuration section '{resolvedName}' should have a 'fullyQualifiedNamespace' property or be a string representing a connection string.");
            }

            var credential = azureComponentFactory.CreateTokenCredential(connectionSection);
            GetFullyQualifiedNamespaceRef(innerTransport) = fullyQualifiedNamespace;
            GetTokenCredentialRef(innerTransport) = credential;
        }
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

    readonly AzureServiceBusTransport innerTransport = new("TransportWillBeInitializedCorrectlyLater", topology) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly };
    static readonly TransportTransactionMode[] supportedTransactionModes = [TransportTransactionMode.ReceiveOnly];
}