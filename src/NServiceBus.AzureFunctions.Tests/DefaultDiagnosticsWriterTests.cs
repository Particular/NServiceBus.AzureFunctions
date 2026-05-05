namespace NServiceBus.AzureFunctions.Tests;

using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using NUnit.Framework;

[TestFixture]
public class DefaultDiagnosticsWriterTests
{
    [Test]
    public void Send_only_endpoint_configures_no_op_diagnostics_writer_by_default()
    {
        var builder = FunctionsApplication.CreateBuilder([]);

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(
            builder,
            "test-endpoint",
            (_, _) => { });

        var writer = endpointConfiguration.GetSettings()
            .GetOrDefault<Func<string, CancellationToken, Task>>(HostDiagnosticsWriterSettingsKey);

        Assert.That(writer, Is.Not.Null);
        Assert.That(writer!("any-diagnostics-payload", CancellationToken.None), Is.SameAs(Task.CompletedTask));
    }

    [Test]
    public void User_supplied_diagnostics_writer_replaces_the_default()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        Func<string, CancellationToken, Task> userWriter = (_, _) => Task.CompletedTask;

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(
            builder,
            "test-endpoint",
            (config, _) => config.CustomDiagnosticsWriter(userWriter));

        var registered = endpointConfiguration.GetSettings()
            .GetOrDefault<Func<string, CancellationToken, Task>>(HostDiagnosticsWriterSettingsKey);

        Assert.That(registered, Is.SameAs(userWriter));
    }

    const string HostDiagnosticsWriterSettingsKey = "HostDiagnosticsWriter";
}