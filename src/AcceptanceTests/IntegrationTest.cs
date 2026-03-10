namespace NServiceBus.AzureFunctions.AcceptanceTests;

using System.Net.Http.Json;
using global::IntegrationTest.Contracts;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
[Parallelizable(ParallelScope.Fixtures)]
public class IntegrationTest
{
    static readonly string AppBaseUrl;

    static IntegrationTest()
    {
        var appHostName = Environment.GetEnvironmentVariable("INTEGRATION_APP_HOSTNAME");
        AppBaseUrl = appHostName is not null ? $"https://{appHostName}" : "http://localhost:7071";
    }

    [SetUp]
    public async Task Setup()
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(2);

        var versionUrl = $"{AppBaseUrl}/api/testing";

        const int timeout = 60;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        try
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {

                    var info = await http.GetFromJsonAsync<InfoResult>(versionUrl, cts.Token);

                    if (info is not null && info.Uptime > TimeSpan.Zero)
                    {
                        return;
                    }

                    await TestContext.Error.WriteLineAsync($"Got null from {versionUrl}, will retry after 2s delay");
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    await TestContext.Error.WriteLineAsync("Got cancellation signal");
                    throw;
                }
                catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException or IOException or ObjectDisposedException)
                {
                    await TestContext.Error.WriteLineAsync($"Got \"{e.GetType().Name}: {e.Message}\" accessing {versionUrl}, will retry after 2s delay");
                }
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
        }

        throw new Exception($"/api/testing failed to respond within {timeout}s");
    }

    [Test]
    public async Task RunIntegrationTest()
    {
        using var http = new HttpClient();

        var invokeUrl = $"{AppBaseUrl}/api/SalesApi";

        _ = await http.GetStringAsync(invokeUrl);

        var result = await GetResults("SubmitOrder", 3);

        Approver.Verify(result);
    }

    static async Task<Payload> GetResults(string testName, int expectedMessages)
    {
        var dataUrl = $"{AppBaseUrl}/api/testing/data/{testName}";
        var http = new HttpClient();
        const int timeout = 30;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
        Payload? payload = null;
        int attempts = 0;

        try
        {
            while (true)
            {
                attempts++;
                payload = await http.GetFromJsonAsync<Payload>(dataUrl, cts.Token);
                if (payload?.MessagesReceived.Length >= expectedMessages)
                {
                    return payload;
                }

                await Task.Delay(1000, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            var msg = $"Test {timeout}s timeout elapsed after {attempts} get attempts. ";
            var payloadInfo = payload is null
                ? "No payload was received"
                : $"Last payload contained {payload.MessagesReceived} messages, but expecting {expectedMessages}.";
            throw new Exception(msg + payloadInfo);
        }
        finally
        {
            _ = await http.DeleteAsync(dataUrl);
        }
    }

    [TearDown]
    public async Task GetErrors()
    {
        using var http = new HttpClient();
        var errorsUrl = $"{AppBaseUrl}/api/testing/errors";
        await TestContext.Error.WriteLineAsync("Fetching exception traces from site");
        var errors = await http.GetStringAsync(errorsUrl);
        await TestContext.Error.WriteLineAsync(errors);
    }
}
