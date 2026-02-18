namespace IntegrationTest.Infrastructure;

using System.Reflection;
using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

class TestDataFunction(GlobalTestStorage storage)
{
    static readonly string InformationalVersion;
    static readonly DateTime StartTime;

    static TestDataFunction()
    {
        InformationalVersion = typeof(TestDataFunction).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .First()
            .InformationalVersion;

        StartTime = DateTime.UtcNow;
    }

    [Function(nameof(GetTestData))]
    public Payload GetTestData([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "testing/data/{testName}")] HttpRequestData _, string testName)
    {
        var payload = storage.CreatePayload(testName);
        return payload;
    }

    [Function(nameof(ClearTestData))]
    public IActionResult ClearTestData([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "testing/data/{testName}")] HttpRequestData _, string testName)
    {
        storage.Clear(testName);
        return new OkResult();
    }

    [Function(nameof(GetInfo))]
    public InfoResult GetInfo([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "testing")] HttpRequestData _)
    {
        var result = new InfoResult(InformationalVersion, DateTime.UtcNow - StartTime);
        return result;
    }

    [Function(nameof(GetErrors))]
    public ExceptionInfo[] GetErrors([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "testing/errors")] HttpRequestData _)
        => ExceptionTrackingMiddleware.GetErrors();
}