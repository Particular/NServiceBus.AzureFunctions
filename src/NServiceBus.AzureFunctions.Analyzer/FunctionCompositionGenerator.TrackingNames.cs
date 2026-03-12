namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class FunctionCompositionGenerator
{
    internal static class TrackingNames
    {
        public const string HostProject = nameof(HostProject);
        public const string LocalFunctions = nameof(LocalFunctions);
        public const string Composition = nameof(Composition);

        public static string[] All => [HostProject, LocalFunctions, Composition];
    }
}