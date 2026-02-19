using System.Text.Json;
using IntegrationTest;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.JsonWriterOptions = new JsonWriterOptions { Indented = true };
});
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.AddNServiceBusFunctions();

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
