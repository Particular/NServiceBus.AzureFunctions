namespace IntegrationTests;

using System.Diagnostics;
using System.Net.Http.Json;
using global::IntegrationTest.Contracts;
using NUnit.Framework;

class IntegrationTestUtil
{
    static readonly string AppBaseUrl;
    static readonly HttpClient http = new();

    static IntegrationTestUtil()
    {
        var appHostName = Environment.GetEnvironmentVariable("INTEGRATION_APP_HOSTNAME");
        AppBaseUrl = appHostName is not null ? $"https://{appHostName}" : "http://localhost:7071";
    }

    public static async Task WaitForAppToBeReady()
    {
        http.Timeout = TimeSpan.FromSeconds(5);

        var versionUrl = $"{AppBaseUrl}/api/testing";

        var timeout = TimeSpan.FromMinutes(5);
        var stopwatch = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {

                    var info = await http.GetFromJsonAsync<InfoResult>(versionUrl, cts.Token);

                    if (info is not null && info.Uptime > TimeSpan.Zero)
                    {
                        await TestContext.Error.WriteLineAsync($"Got successful info in {stopwatch.Elapsed}");
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
                    await TestContext.Error.WriteLineAsync($"Got \"{e.GetType().Name}: {e.Message}\" accessing {versionUrl}, will retry after 5s delay");
                }
                await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
        }

        throw new Exception($"/api/testing failed to respond within {timeout}");
    }

    public static async Task InvokeApi(string apiPath)
    {
        if (!apiPath.StartsWith('/'))
        {
            throw new Exception("API path must start with /");
        }

        var invokeUrl = $"{AppBaseUrl}{apiPath}";

        _ = await http.GetStringAsync(invokeUrl);
    }

    public static async Task<Payload> GetResults(string testName, int expectedMessages)
    {
        var dataUrl = $"{AppBaseUrl}/api/testing/data/{testName}";
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

    public static async Task TryWriteErrors()
    {
        var errorsUrl = $"{AppBaseUrl}/api/testing/errors";
        await TestContext.Error.WriteLineAsync("Fetching exception traces from site");
        var errors = await http.GetStringAsync(errorsUrl);
        await TestContext.Error.WriteLineAsync(errors);
    }
}