namespace NServiceBus.AzureFunctions.Tests;

using System.Text.RegularExpressions;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public partial class ApiApprovals
{
    [Test]
    public void ApproveAzureServiceBusComponentApi()
    {
        var publicApi = typeof(AzureServiceBusServerlessTransport).Assembly.GeneratePublicApi(new ApiGeneratorOptions()
        {
            ExcludeAttributes = ["System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute"]
        });

        Approver.Verify(publicApi);
    }

    [GeneratedRegex(@"(?<Prefix>GeneratedFunctionRegistrations_\w+)(?<Hash>[0-9a-f]{16})", RegexOptions.Compiled)]
    private static partial Regex GeneratedClassScrubberRegex();

    static string ScrubGeneratedClassName(string className) => GeneratedClassScrubberRegex().Replace(className, "${Prefix}{GENERATED_HASH}");

    [Test]
    public void ApproveFunctionsComponentApi()
    {
        var publicApi = typeof(NServiceBusFunctionAttribute).Assembly.GeneratePublicApi(new ApiGeneratorOptions()
        {
            ExcludeAttributes = ["System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute"]
        });

        Approver.Verify(publicApi, scrubber: ScrubGeneratedClassName);
    }
}