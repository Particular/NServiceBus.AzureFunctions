namespace NServiceBus;

using Microsoft.Extensions.DependencyInjection;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromEndpoint(string endpointName) : FromKeyedServicesAttribute(endpointName);