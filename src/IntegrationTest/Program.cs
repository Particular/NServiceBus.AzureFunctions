using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>options.IncludeScopes = true);
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.AddNServiceBusFunctions();

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
