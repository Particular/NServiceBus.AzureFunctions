namespace IntegrationTest.Infrastructure;

using NServiceBus.Pipeline;

public class IncomingTestBehavior(TestStorage storage) : IBehavior<IInvokeHandlerContext, IInvokeHandlerContext>
{
    public async Task Invoke(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
    {
        var testCaseName = context.MessageHeaders.GetValueOrDefault("TestCaseName") ?? "<unknown-test>";
        context.Extensions.Set("TestCaseName", testCaseName);

        var orderString = context.MessageHeaders.GetValueOrDefault("TestStorageOrder");
        var order = int.TryParse(orderString, out var storageOrder) ? storageOrder : 0;
        context.Extensions.Set("TestStorageOrder", order);

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
        if (context.Extensions.TryGet<string>("TestCaseName", out var testCaseName))
        {
            context.Headers.Add("TestCaseName", testCaseName);
        }

        if (!context.Extensions.TryGet<int>("TestStorageOrder", out var order))
        {
            order = 0;
        }

        order++;

        context.Headers.Add("TestStorageOrder", order.ToString());

        return next(context);
    }
}