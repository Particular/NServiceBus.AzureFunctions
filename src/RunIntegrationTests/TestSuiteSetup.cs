using IntegrationTests;
using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.Fixtures)]

[SetUpFixture]
class TestSuiteSetup
{
    [OneTimeSetUp]
    public Task Setup() => IntegrationTestUtil.WaitForAppToBeReady();

    [OneTimeTearDown]
    public Task GetErrors() => IntegrationTestUtil.TryWriteErrors();
}
