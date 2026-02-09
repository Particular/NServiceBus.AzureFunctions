namespace IntegrationTest.Infrastructure;

using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

class TestDataFunction(GlobalTestStorage storage)
{
    [Function(nameof(GetTestData))]
    public Payload GetTestData([HttpTrigger(AuthorizationLevel.Function, "get", Route = "testing/data/{testName}")] HttpRequestData _, string testName)
    {
        var payload = storage.CreatePayload(testName);
        return payload;
    }

    [Function(nameof(ClearTestData))]
    public IActionResult ClearTestData([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "testing/data/{testName}")] HttpRequestData _, string testName)
    {
        storage.Clear(testName);
        return new OkResult();
    }
}