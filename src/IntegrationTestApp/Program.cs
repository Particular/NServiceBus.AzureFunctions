using IntegrationTest.Shared;
using IntegrationTest.Shared.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);
builder.UseMiddleware<ExceptionTrackingMiddleware>();

builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<GlobalTestStorage>();
builder.Services.AddSingleton(new MyComponent("global"));

builder.AddNServiceBusFunctions();

var host = builder.Build();

await host.RunAsync();
