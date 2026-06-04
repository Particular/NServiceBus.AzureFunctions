namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class SendOnlyEndpointGenerator
{
    internal static class TrackingNames
    {
        public const string Extraction = nameof(Extraction);
        public const string Diagnostics = nameof(Diagnostics);
        public const string SendOnlyEndpoints = nameof(SendOnlyEndpoints);
        public const string AssemblyClassName = nameof(AssemblyClassName);
        public const string Combined = nameof(Combined);

        public static string[] All => [Extraction, Diagnostics, SendOnlyEndpoints, AssemblyClassName, Combined];
    }
}