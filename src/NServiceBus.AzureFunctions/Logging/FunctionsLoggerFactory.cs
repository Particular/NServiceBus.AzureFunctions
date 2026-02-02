namespace NServiceBus.AzureFunctions;

using System.Collections.Concurrent;
using NServiceBus.Logging;
using ILoggerFactory = NServiceBus.Logging.ILoggerFactory;

public class FunctionsLoggerFactory : ILoggerFactory
{
    FunctionsLoggerFactory()
    {
    }

    public static readonly FunctionsLoggerFactory Instance = new();

    readonly ConcurrentDictionary<string, FunctionsLogger> loggers = new();
    readonly ConcurrentDictionary<string, NameSlot> slots = new();
    readonly AsyncLocal<NameSlot> slot = new();
    Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory;

    public ILog GetLogger(Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type.FullName);
        return loggers.GetOrAdd(type.FullName, name => new FunctionsLogger(slot, loggerFactory, name));
    }

    public ILog GetLogger(string name) => loggers.GetOrAdd(name, newName => new FunctionsLogger(slot, loggerFactory, newName));

    public void SetLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) => this.loggerFactory = loggerFactory;

    void Flush()
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        foreach (var logger in loggers)
        {
            logger.Value.Flush(loggerFactory);
        }
    }

    public NameScope PushName(string name)
    {
        var previous = slot.Value;
        slot.Value = slots.GetOrAdd(name, static endpoint => new NameSlot
        {
            Format = "Endpoint = {Endpoint}",
            Args = [endpoint],
        });
        return new NameScope(this, slot, previous);
    }

    public readonly struct NameScope(FunctionsLoggerFactory factory, AsyncLocal<NameSlot> slot, NameSlot? previous) : IDisposable
    {
        public void Flush() => factory.Flush();

        public void Dispose() => slot.Value = previous!;
    }
}