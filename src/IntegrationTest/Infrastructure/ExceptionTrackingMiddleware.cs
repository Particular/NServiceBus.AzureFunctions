namespace IntegrationTest.Infrastructure;

using System.Collections.Concurrent;
using Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

public class ExceptionTrackingMiddleware : IFunctionsWorkerMiddleware
{
    static ConcurrentQueue<ExceptionInfo> collectedExceptions = [];

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {

            var info = new ExceptionInfo(context.FunctionDefinition.Name, e.GetType().FullName ?? "<UnknownType>", e.Message, e.ToString());
            collectedExceptions.Enqueue(info);
            throw;
        }
    }

    public static ExceptionInfo[] GetErrors()
    {
        while (collectedExceptions.Count > 20)
        {
            _ = collectedExceptions.TryDequeue(out _);
        }

        return [.. collectedExceptions];
    }

}