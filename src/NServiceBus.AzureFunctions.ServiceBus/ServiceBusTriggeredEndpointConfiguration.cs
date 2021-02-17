﻿namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using AzureFunctions.ServiceBus;
    using Logging;
    using Microsoft.Azure.WebJobs;
    using Serialization;

    /// <summary>
    /// Represents a serverless NServiceBus endpoint running within an AzureServiceBus trigger.
    /// </summary>
    public class ServiceBusTriggeredEndpointConfiguration
    {
        static ServiceBusTriggeredEndpointConfiguration()
        {
            LogManager.UseFactory(FunctionsLoggerFactory.Instance);
        }

        /// <summary>
        /// Creates a serverless NServiceBus endpoint running within an Azure Service Bus trigger.
        /// </summary>
        public ServiceBusTriggeredEndpointConfiguration(string endpointName, string connectionStringName = null, Action<AzureServiceBusTransport> azureTransportConfiguration = null)
        {
            EndpointConfiguration = new EndpointConfiguration(endpointName);

            recoverabilityPolicy.SendFailedMessagesToErrorQueue = true;
            EndpointConfiguration.Recoverability().CustomPolicy(recoverabilityPolicy.Invoke);

            // Disable diagnostics by default as it will fail to create the diagnostics file in the default path.
            // Can be overriden by ServerlessEndpointConfiguration.LogDiagnostics().
            EndpointConfiguration.CustomDiagnosticsWriter(_ => Task.CompletedTask);

            // 'WEBSITE_SITE_NAME' represents an Azure Function App and the environment variable is set when hosting the function in Azure.
            var functionAppName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? Environment.MachineName;
            EndpointConfiguration.UniquelyIdentifyRunningInstance()
                .UsingCustomDisplayName(functionAppName)
                .UsingCustomIdentifier(DeterministicGuid.Create(functionAppName));

            // Look for license as an environment variable
            var licenseText = Environment.GetEnvironmentVariable("NSERVICEBUS_LICENSE");
            if (!string.IsNullOrWhiteSpace(licenseText))
            {
                EndpointConfiguration.License(licenseText);
            }

            var connectionString =
                Environment.GetEnvironmentVariable(connectionStringName ?? DefaultServiceBusConnectionName);
            var serviceBusTransport = new AzureServiceBusTransport(connectionString);
            azureTransportConfiguration?.Invoke(serviceBusTransport);

            serverlessTransport = new ServerlessTransport<AzureServiceBusTransport>(serviceBusTransport);
            Routing = EndpointConfiguration.UseTransport(serverlessTransport);

            var recoverability = AdvancedConfiguration.Recoverability();
            recoverability.Immediate(settings => settings.NumberOfRetries(5));
            recoverability.Delayed(settings => settings.NumberOfRetries(3));

            EndpointConfiguration.UseSerialization<NewtonsoftSerializer>();
        }

        /// <summary>
        /// Routing configuration.
        /// </summary>
        public RoutingSettings Routing { get; }

        internal EndpointConfiguration EndpointConfiguration { get; }
        internal PipelineInvoker PipelineInvoker => serverlessTransport.MainReceiver;

        /// <summary>
        /// Gives access to the underlying endpoint configuration for advanced configuration options.
        /// </summary>
        public EndpointConfiguration AdvancedConfiguration => EndpointConfiguration;

        /// <summary>
        /// Attempts to derive the required configuration parameters automatically from the Azure Functions related attributes via
        /// reflection.
        /// </summary>
        public static ServiceBusTriggeredEndpointConfiguration FromAttributes()
        {
            var configuration = TriggerDiscoverer.TryGet<ServiceBusTriggerAttribute>();
            if (configuration != null)
            {
                return new ServiceBusTriggeredEndpointConfiguration(configuration.QueueName, configuration.Connection);
            }

            throw new Exception(
                $"Unable to automatically derive the endpoint name from the ServiceBusTrigger attribute. Make sure the attribute exists or create the {nameof(ServiceBusTriggeredEndpointConfiguration)} with the required parameter manually.");
        }

        /// <summary>
        /// Define the serializer to be used.
        /// </summary>
        public SerializationExtensions<T> UseSerialization<T>() where T : SerializationDefinition, new()
        {
            return EndpointConfiguration.UseSerialization<T>();
        }

        /// <summary>
        /// Disables moving messages to the error queue even if an error queue name is configured.
        /// </summary>
        public void DoNotSendMessagesToErrorQueue()
        {
            recoverabilityPolicy.SendFailedMessagesToErrorQueue = false;
        }

        /// <summary>
        /// Logs endpoint diagnostics information to the log. Diagnostics are logged on level <see cref="LogLevel.Info" />.
        /// </summary>
        public void LogDiagnostics()
        {
            EndpointConfiguration.CustomDiagnosticsWriter(diagnostics =>
            {
                LogManager.GetLogger("StartupDiagnostics").Info(diagnostics);
                return Task.CompletedTask;
            });
        }

        ServerlessTransport<AzureServiceBusTransport> serverlessTransport;
        readonly ServerlessRecoverabilityPolicy recoverabilityPolicy = new ServerlessRecoverabilityPolicy();
        internal const string DefaultServiceBusConnectionName = "AzureWebJobsServiceBus";
    }
}
