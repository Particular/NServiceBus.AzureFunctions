namespace IntegrationTests;

using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class SalesApiTest
{
    [Test]
    public async Task RunIntegrationTest()
    {
        await IntegrationTestUtil.InvokeApi("/api/SalesApi");
        var result = await IntegrationTestUtil.GetResults("SubmitOrder", 3);
        Approver.Verify(result);
    }
}
