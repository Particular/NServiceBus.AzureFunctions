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
using NUnit.Framework;
using Particular.AnalyzerTesting;

[SetUpFixture]
public class SetUpFixture
{
    const string CommonUsings = """
        global using System.Threading;
        global using System.Threading.Tasks;
        global using Microsoft.Azure.Functions.Worker;
        global using Azure.Messaging.ServiceBus;
        global using Microsoft.Extensions.Configuration;
        global using Microsoft.Extensions.Hosting;
        global using NServiceBus;
        """;

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
        MetadataReference.CreateFromFile(typeof(AzureServiceBusMessageProcessor).GetTypeInfo().Assembly.Location)
    ];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        AnalyzerTest.ConfigureAllAnalyzerTests(test => test
            .AddReferences(ProjectReferences)
            .WithCommonUsings(CommonUsings.Split(";", StringSplitOptions.RemoveEmptyEntries)));

        SourceGeneratorTest.ConfigureAllSourceGeneratorTests(test => test
            .AddReferences(ProjectReferences)
            .WithSource(CommonUsings, "GlobalUsings.cs"));
    }
}