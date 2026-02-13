using System.Net.Http.Json;
using IntegrationTest.Contracts;
using NUnit.Framework;

[SetUpFixture]
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

    [OneTimeTearDown]
    public async Task GetErrors()
    {
        using var http = new HttpClient();
        var errorsUrl = $"{AppBaseUrl}/api/testing/errors";
        await TestContext.Error.WriteLineAsync("Fetching exception traces from site");
        var errors = await http.GetStringAsync(errorsUrl);
        await TestContext.Error.WriteLineAsync(errors);
    }
}