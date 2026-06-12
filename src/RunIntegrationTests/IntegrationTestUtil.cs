namespace IntegrationTests;

using System.Diagnostics;
using System.Net.Http.Json;
using global::IntegrationTest.Contracts;
using NUnit.Framework;

class IntegrationTestUtil
{
    static readonly string AppBaseUrl;
    static readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static IntegrationTestUtil()
    {
        var appHostName = Environment.GetEnvironmentVariable("INTEGRATION_APP_HOSTNAME");
        AppBaseUrl = appHostName is not null ? $"https://{appHostName}" : "http://localhost:7071";
    }

    public static async Task WaitForAppToBeReady()
    {
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
                    var info = await GetInfo(versionUrl, cts.Token);

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
                catch (OperationCanceledException e)
                {
                    await TestContext.Error.WriteLineAsync($"Got \"{e.GetType().Name}: readiness probe timed out after 5 seconds\" accessing {versionUrl}, will retry after 5s delay");
                }
                catch (Exception e) when (e is HttpRequestException or TimeoutException or IOException or ObjectDisposedException)
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

    static async Task<InfoResult?> GetInfo(string versionUrl, CancellationToken cancellationToken)
    {
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptCts.CancelAfter(TimeSpan.FromSeconds(5));

        return await http.GetFromJsonAsync<InfoResult>(versionUrl, attemptCts.Token);
    }

    public static async Task InvokeApi(string apiPath)
    {
        if (!apiPath.StartsWith('/'))
        {
            throw new Exception("API path must start with /");
        }

        var invokeUrl = $"{AppBaseUrl}{apiPath}";
        var timeout = TimeSpan.FromMinutes(2);
        var stopwatch = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(timeout);

        while (true)
        {
            try
            {
                using var response = await http.GetAsync(invokeUrl, cts.Token);
                response.EnsureSuccessStatusCode();
                return;
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new Exception($"{invokeUrl} failed to respond within {timeout}");
            }
            catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException or IOException or ObjectDisposedException)
            {
                await TestContext.Error.WriteLineAsync($"Got \"{e.GetType().Name}: {e.Message}\" accessing {invokeUrl} after {stopwatch.Elapsed}, will retry after 5s delay");
                await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
            }
        }
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