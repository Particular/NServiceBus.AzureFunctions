namespace NServiceBus.AzureFunctions.Tests;

using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class ApiApprovals
{
    [Test]
    public void ApprovaAzureServiceBusComponentApi()
    {
        var publicApi = typeof(AzureServiceBusServerlessTransport).Assembly.GeneratePublicApi(new ApiGeneratorOptions()
        {
            ExcludeAttributes = ["System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute"]
        });

        Approver.Verify(publicApi);
    }
}