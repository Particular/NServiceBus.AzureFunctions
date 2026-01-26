using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.AzureFunctions.AzureServiceBus;

var builder = FunctionsApplication.CreateBuilder(args);
