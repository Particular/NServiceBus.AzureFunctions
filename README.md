# NServiceBus.AzureFunctions

## Running the integration test application locally

Create a local.settings.json file in the root of the project. The file should contain the following content, replacing `YourConnectionString` with the connection string to your Azure Service Bus namespace.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsServiceBus": "YourConnectionString",
    "BillingPrefix": "billing"
  }
}
```