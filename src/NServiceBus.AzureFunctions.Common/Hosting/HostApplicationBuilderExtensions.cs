namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

/// <summary>
/// Extension methods for <see cref="FunctionsApplicationBuilder"/> that integrate NServiceBus
/// Azure Functions composition.
/// </summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Wires up the NServiceBus functions and send-only endpoint registrations discovered by
    /// the source generators into the supplied <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// This method is intentionally a no-op. The body is replaced at compile time by an
    /// interceptor emitted by the <c>FunctionCompositionInterceptor</c> source generator, which
    /// delegates to the generated <c>NServiceBusGeneratedFunctionsComposition.Register</c> method.
    /// </remarks>
    /// <param name="builder">The Functions application builder to compose registrations into.</param>
    public static void AddNServiceBusFunctions(this FunctionsApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Properties.TryAdd("NServiceBus.AzureFunctions.AddNServiceBusFunctions", true))
        {
            throw new Exception("`AddNServiceBusFunctions` can only be used once on the same functions application builder instance because subsequent calls would override each other.");
        }
        // Intentionally empty. Replaced at compile time by the FunctionCompositionInterceptor.
    }
}