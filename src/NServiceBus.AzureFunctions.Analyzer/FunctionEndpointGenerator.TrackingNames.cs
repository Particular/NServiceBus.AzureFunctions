namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class FunctionEndpointGenerator
{
    static class TrackingNames
    {
        public const string Extraction = nameof(Extraction);
        public const string Diagnostics = nameof(Diagnostics);
        public const string Functions = nameof(Functions);
        public const string AssemblyClassName = nameof(AssemblyClassName);
        public const string Combined = nameof(Combined);
    }
}