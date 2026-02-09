namespace IntegrationTest.Infrastructure;

using System.Collections.Concurrent;
using Contracts;

public class GlobalTestStorage
{
    readonly ConcurrentDictionary<string, List<MessageReceived>> data = [];

    public void Clear(string testName) => data.TryRemove(testName, out _);

    public void Add(string testName, MessageReceived msg)
    {
        var list = data.GetOrAdd(testName, _ = new List<MessageReceived>());
        list.Add(msg);
    }

    public Payload CreatePayload(string testName)
    {
        var list = data.GetValueOrDefault(testName);
        if (list is null)
        {
            return new Payload([]);
        }

        var sortedMessages = list
            .OrderBy(m => m.Order)
                .ThenBy(m => m.MessageType)
                .ThenBy(m => m.SendingEndpoint)
                .ThenBy(m => m.ReceivingEndpoint)
            .ToArray();

        return new Payload(sortedMessages);
    }
}

public class TestStorage(string endpointName, GlobalTestStorage globalStorage)
{
    public void LogMessage<T>(string testName, T message, IMessageHandlerContext context)
        where T : class
    {
        var sendingEndpoint = context.MessageHeaders[Headers.OriginatingEndpoint] ?? "<unknown>";
        var storageOrder = context.Extensions.Get<int>("TestStorageOrder");
        var rec = new MessageReceived(message.GetType().FullName!, storageOrder, sendingEndpoint, endpointName);
        globalStorage.Add(testName, rec);
    }
}