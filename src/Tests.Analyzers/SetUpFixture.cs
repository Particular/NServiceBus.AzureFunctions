namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AzureFunctions.AzureServiceBus;
using NServiceBus.Transport.AzureServiceBus;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[SetUpFixture]
public class SetUpFixture
{
    static readonly ImmutableList<PortableExecutableReference> ProjectReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Private.CoreLib").Location),
        MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Azure.Functions.Worker").Location),
        MetadataReference.CreateFromFile(typeof(FunctionAttribute).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Azure.Functions.Worker.Core").Location),
        MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Azure.Functions.Worker.Extensions.Abstractions").Location),
        MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Azure.Functions.Worker.Extensions.ServiceBus").Location),
        MetadataReference.CreateFromFile(Assembly.Load("Azure.Messaging.ServiceBus").Location),
        MetadataReference.CreateFromFile(typeof(ServiceBusTriggerAttribute).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IConfiguration).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IHostEnvironment).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ServiceCollectionServiceExtensions).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(EndpointConfiguration).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(NServiceBusFunctionAttribute).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(AzureServiceBusMessageProcessor).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(TopicTopology).GetTypeInfo().Assembly.Location),
        MetadataReference.CreateFromFile(typeof(HostApplicationBuilderExtensions).GetTypeInfo().Assembly.Location)
    ];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        AnalyzerTest.ConfigureAllAnalyzerTests(test => test
            .AddReferences(ProjectReferences));

        SourceGeneratorTest.ConfigureAllSourceGeneratorTests(test => test
            .AddReferences(ProjectReferences));
    }
}