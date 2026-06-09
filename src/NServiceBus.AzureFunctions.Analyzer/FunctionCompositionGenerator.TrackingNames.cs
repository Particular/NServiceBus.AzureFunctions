namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class FunctionCompositionGenerator
{
    internal static class TrackingNames
    {
        public const string LocalFunctions = nameof(LocalFunctions);
        public const string LocalSendOnlyEndpoints = nameof(LocalSendOnlyEndpoints);
        public const string AddNServiceBusFunctionsInvocations = nameof(AddNServiceBusFunctionsInvocations);
        public const string Composition = nameof(Composition);

        public static string[] All => [LocalFunctions, LocalSendOnlyEndpoints, AddNServiceBusFunctionsInvocations, Composition];
    }
}