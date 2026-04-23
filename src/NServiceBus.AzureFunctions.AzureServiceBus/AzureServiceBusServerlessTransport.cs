namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using AzureFunctions.AzureServiceBus;
using BitFaster.Caching.Lru;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus transport tailored for endpoints hosted in Azure Functions. Receiving is
/// driven by the Service Bus trigger; dispatch and topology management remain with the transport.
/// </summary>
public class AzureServiceBusServerlessTransport : TransportDefinition
{
    /// <summary>
    /// Creates a new transport using the supplied <paramref name="topology"/>. The connection is
    /// resolved from configuration during <see cref="Initialize"/> via <see cref="ConnectionName"/>.
    /// </summary>
    /// <param name="topology">The topic topology describing how events are published and subscribed to.</param>
    public AzureServiceBusServerlessTransport(TopicTopology topology) : base(TransportTransactionMode.ReceiveOnly,
        supportsDelayedDelivery: true,
        supportsPublishSubscribe: true,
        supportsTTBR: true)
    {
        innerTransport = new("TransportWillBeInitializedCorrectlyLater", topology) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly };
        AutoForwardDeadLetteredMessagesToErrorQueue = true;
    }

    /// <summary>
    /// Enables auto-forwarding of dead-lettered messages to the configured error queue. (Enabled by default)
    /// </summary>
    /// <remarks>
    /// This option only affects queues created by the transport during infrastructure setup. It applies to transport-created endpoint queues,
    /// including instance-specific queues, and excludes the error queue itself to avoid self-forwarding loops.
    /// </remarks>
    public bool AutoForwardDeadLetteredMessagesToErrorQueue
    {
        get => innerTransport.AutoForwardDeadLetteredMessagesToErrorQueue;
        set => innerTransport.AutoForwardDeadLetteredMessagesToErrorQueue = value;
    }

    /// <summary>
    /// Enables entity partitioning when creating queues and topics.
    /// </summary>
    /// <remarks>
    /// This option only affects entities created by the transport during infrastructure setup.
    /// It does not modify entities that already exist in the namespace.
    /// </remarks>
    public bool EnablePartitioning
    {
        get => innerTransport.EnablePartitioning;
        set => innerTransport.EnablePartitioning = value;
    }

    /// <summary>
    /// The maximum size used when creating queues and topics in GB.
    /// </summary>
    /// <remarks>
    /// This option only affects entities created by the transport during infrastructure setup.
    /// It does not modify entities that already exist in the namespace.
    /// </remarks>
    public int EntityMaximumSize
    {
        get => innerTransport.EntityMaximumSize;
        set => innerTransport.EntityMaximumSize = value;
    }

    /// <summary>
    /// Configures hierarchy namespace support.
    /// </summary>
    /// <remarks>
    /// This setting affects both infrastructure setup and runtime behavior by shaping queue names,
    /// topic names, and destination resolution for the endpoint.
    /// </remarks>
    public HierarchyNamespaceOptions HierarchyNamespaceOptions
    {
        get => innerTransport.HierarchyNamespaceOptions;
        set => innerTransport.HierarchyNamespaceOptions = value;
    }

    /// <summary>
    /// The configuration key used to resolve the Azure Service Bus connection. Defaults to
    /// <c>AzureWebJobsServiceBus</c>, matching the setting used by Service Bus triggers.
    /// </summary>
    /// <remarks>The resolved value may be a connection string, or a configuration section containing a <c>fullyQualifiedNamespace</c> entry for token-credential authentication.</remarks>
    public string ConnectionName { get; set; } = DefaultServiceBusConnectionName;

    /// <inheritdoc />
    public override async Task<TransportInfrastructure> Initialize(
        HostSettings hostSettings,
        ReceiveSettings[] receivers,
        string[] sendingAddresses,
        CancellationToken cancellationToken = default)
    {
        if (!hostSettings.SupportsDependencyInjection)
        {
            throw new Exception("AzureServiceBusServerlessTransport requires a host that provides an initialized service provider so it can resolve dependencies.");
        }

        if (hostSettings.IsRawMode)
        {
            throw new Exception("AzureServiceBusServerlessTransport must be hosted inside an NServiceBus endpoint; raw mode is not supported.");
        }

        var configuredTransport = ConfigureTransportConnection(
            ConnectionName,
            hostSettings.ServiceProvider.GetRequiredService<IConfiguration>(),
            innerTransport,
            hostSettings.ServiceProvider.GetRequiredService<AzureComponentFactory>());

        var baseTransportInfrastructure = await configuredTransport.Initialize(
                hostSettings,
                receivers,
                sendingAddresses,
                cancellationToken)
            .ConfigureAwait(false);

        var serverlessTransportInfrastructure = new ServerlessTransportInfrastructure(baseTransportInfrastructure,
            receiver => new PipelineInvokingMessageProcessor(receiver, new FastConcurrentLru<string, bool>(1_000), hostSettings.ServiceProvider.GetRequiredService<ILogger<PipelineInvokingMessageProcessor>>()));

        var isSendOnly = hostSettings.CoreSettings.GetOrDefault<bool>(SendOnlyConfigKey);

        if (!isSendOnly)
        {
            MessageProcessor = (PipelineInvokingMessageProcessor)serverlessTransportInfrastructure.Receivers[MainReceiverId];
        }

        return serverlessTransportInfrastructure;
    }


    /// <inheritdoc />
    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => [TransportTransactionMode.ReceiveOnly];

    /// <inheritdoc />
    protected override void ConfigureServicesCore(IServiceCollection services) => innerTransport.ConfigureServices(services);

    internal PipelineInvokingMessageProcessor? MessageProcessor { get; private set; }

    static AzureServiceBusTransport ConfigureTransportConnection(
        string connectionName,
        IConfiguration configuration,
        AzureServiceBusTransport transport,
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
            GetConnectionStringRef(transport) = connectionSection.Value;
        }
        else
        {
            string? fullyQualifiedNamespace = connectionSection["fullyQualifiedNamespace"];
            if (string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
            {
                throw new Exception($"Configuration section '{resolvedName}' should have a 'fullyQualifiedNamespace' property or be a string representing a connection string.");
            }

            var credential = azureComponentFactory.CreateTokenCredential(connectionSection);
            GetFullyQualifiedNamespaceRef(transport) = fullyQualifiedNamespace;
            GetTokenCredentialRef(transport) = credential;
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

    readonly AzureServiceBusTransport innerTransport;

    const string MainReceiverId = "Main";
    const string SendOnlyConfigKey = "Endpoint.SendOnly";
    const string DefaultServiceBusConnectionName = "AzureWebJobsServiceBus";
}
