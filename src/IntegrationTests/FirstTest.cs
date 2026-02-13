namespace IntegrationTests;

using System.Net.Http.Json;
using IntegrationTest.Contracts;
using NUnit.Framework;
using Particular.Approvals;

public class FirstTest
{
    [Test]
    public async Task TestIntegrationTestApp()
    {
        var http = new HttpClient();

        var invokeUrl = $"{SetupFixture.AppBaseUrl}/api/HttpSenderV4";

        _ = await http.GetStringAsync(invokeUrl);

        var result = await GetResults("FirstTest", 4);

        Approver.Verify(result);
    }

    static async Task<Payload> GetResults(string testName, int expectedMessages)
    {
        var dataUrl = $"{SetupFixture.AppBaseUrl}/api/testing/data/{testName}";
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
        catch (OperationCanceledException e) when (cts.Token.IsCancellationRequested)
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
}