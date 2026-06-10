namespace IntegrationTest.Shared.Infrastructure;

using System.Collections.Concurrent;
using Contracts;

public class GlobalTestStorage
{
    readonly ConcurrentDictionary<string, ConcurrentBag<MessageReceived>> data = [];

    public void Clear(string testName) => data.TryRemove(testName, out _);

    public void Add(string testName, MessageReceived msg)
    {
        var bag = data.GetOrAdd(testName, _ => new ConcurrentBag<MessageReceived>());
        bag.Add(msg);
    }

    public Payload CreatePayload(string testName)
    {
        var bag = data.GetValueOrDefault(testName);
        if (bag is null)
        {
            return new Payload([]);
        }

        var sortedMessages = bag
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
        var sendingEndpoint = context.MessageHeaders.GetValueOrDefault(Headers.OriginatingEndpoint, "<unknown>");
        var storageOrder = context.Extensions.Get<int>("TestStorageOrder");

        var rec = new MessageReceived(message.GetType().FullName!, storageOrder, sendingEndpoint, endpointName);
        globalStorage.Add(testName, rec);
    }
}