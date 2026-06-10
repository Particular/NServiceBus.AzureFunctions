namespace IntegrationTest.Shared.Infrastructure;

using System.Threading;
using NServiceBus.Pipeline;

public class IncomingTestBehavior(TestStorage storage) : IBehavior<IInvokeHandlerContext, IInvokeHandlerContext>
{
    public async Task Invoke(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
    {
        var testCaseName = context.MessageHeaders.GetValueOrDefault("TestCaseName") ?? "<unknown-test>";
        var orderString = context.MessageHeaders.GetValueOrDefault("TestStorageOrder");

        var order = int.TryParse(orderString, out var storageOrder) ? storageOrder : 0;
        context.Extensions.Set(new TestStorageContext(testCaseName, order));

        try
        {
            await next(context);
            storage.LogMessage(testCaseName, context.MessageBeingHandled, context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public class OutgoingTestBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
{
    public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
    {
        int order;
        if (context.Extensions.TryGet<TestStorageContext>(out var testStorageContext))
        {
            context.Headers["TestCaseName"] = testStorageContext.TestCaseName;
            order = testStorageContext.NextOutgoingOrder();
        }
        else
        {
            var initialOrder = context.Headers.TryGetValue("TestStorageOrder", out var headerValue) && int.TryParse(headerValue, out var headerOrder)
                ? headerOrder
                : 0;
            order = initialOrder + 1;
        }

        context.Headers["TestStorageOrder"] = order.ToString();

        return next(context);
    }
}

public class TestStorageContext(string testCaseName, int receivedOrder)
{
    int outgoingOrder = receivedOrder;

    public string TestCaseName { get; } = testCaseName;

    public int ReceivedOrder { get; } = receivedOrder;

    public int NextOutgoingOrder() => Interlocked.Increment(ref outgoingOrder);
}
