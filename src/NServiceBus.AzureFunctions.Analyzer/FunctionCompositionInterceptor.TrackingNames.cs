namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class FunctionCompositionInterceptor
{
    internal static class TrackingNames
    {
        public const string AddNServiceBusFunctionsSpec = nameof(AddNServiceBusFunctionsSpec);
        public const string AddNServiceBusFunctionsSpecs = nameof(AddNServiceBusFunctionsSpecs);

        public static string[] All => [AddNServiceBusFunctionsSpec, AddNServiceBusFunctionsSpecs];
    }
}