using System.Net.Http.Json;
using IntegrationTest.Contracts;
using NUnit.Framework;

public class SetupFixture
{
    public static readonly string AppBaseUrl;

    static SetupFixture()
    {
        var appHostName = Environment.GetEnvironmentVariable("INTEGRATION_APP_HOSTNAME");
        AppBaseUrl = appHostName is not null ? $"https://{appHostName}" : "http://localhost:7071";
    }

    [OneTimeSetUp]
    public async Task Setup()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var versionUrl = $"{AppBaseUrl}/api/testing";

        const int timeout = 30;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        while (!cts.IsCancellationRequested)
        {
            var info = await http.GetFromJsonAsync<InfoResult>(versionUrl, cts.Token);

            if (info is not null && info.Uptime > TimeSpan.Zero)
            {
                return;
            }
        }

        Assert.Fail($"/api/testing failed to respond within {timeout}s");
    }
}