# IntegrationTestApp

The purpose of this app is to test that an app using the NServiceBus.AzureFunctions.AzureServiceBus package can be deployed to a real Function App.

The NServiceBus integration in this package is very thin and limited mostly to what is emitted by source generators, which are well-tested. What is left is very difficult to test without mocking too much of the Functions infrastructure to be confident that a passing test provides any real value. Therefore, this project serves as an automated smoke test to ensure that everything works as expected.

## How it works

- The app is composed of multiple projects ([Sales](../IntegrationTest.Sales), [Billing](../IntegrationTest.Billing), [Shipping](../IntegrationTest.Shipping)) representing separate NServiceBus endpoints so that the multi-endpoint configuration can be tested.
- Each endpoint project references:
  - The [NServiceBus.AzureFunctions.AzureServiceBus project](../NServiceBus.AzureFunctions.AzureServiceBus)
  - The Analyzer and CodeFixes projects as analyzers so that analyzers and code fixes can be run locally in the solution. A real-life application would gain these references automatically from the bundled NServiceBus.AzureFunctions.Common package.
  - The [Shared](../IntegrationTest.Shared) project
- There is some duplication in references to Functions SDK packages which is necessary to ensure that the Functions SDK source generators operate on the triggers in each endpoint project.
- The CI workflow creates an Azure Service Bus namespace and a Functions App, and deploys the IntegrationTestApp to the FunctionsApp.
- The [RunIntegrationTests](../RunIntegrationTests) project contains utilities for waiting until the Function App is ready (which can take a minute or two) and then triggering tests to run and collecting the results.
- This [IntegrationTest.Shared](../IntegrationTest.Shared) project contains infrastructure for keeping an in-memory log of messages sent and received by various enpdoints in a way that can be verified using an approval test.

## Guidelines

- The [repository README](/README.md) contains instructions for running the integration test locally, which would be required to update any of the approval tests based on it.
- It is probably better idea to keep a minimialistic approach to test scenarios, as new tests would need to combine handlers spread among many of the integration test's NServiceBus endpoints. A test can't be isolated to within a test class, and the potential for complexity sprawl is large.
