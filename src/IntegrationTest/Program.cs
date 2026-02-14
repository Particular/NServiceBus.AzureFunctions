using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddNServiceBusFunctions();

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
