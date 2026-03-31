namespace NServiceBus.AzureFunctions.Analyzer;

using NServiceBus.Core.Analyzer;

public sealed partial class FunctionEndpointGenerator
{
    /// <summary>
    /// Defines the transport-specific identity for code generation. To add a new transport,
    /// create a <see cref="IIncrementalGenerator"/> that calls
    /// <see cref="InitializeGenerator"/> with a new <see cref="TriggerDefinition"/>,
    /// and implement a corresponding message processor and AddNServiceBusFunction extension.
    /// Transport filtering uses <c>TryGet</c> in the parser: if the trigger attribute type is not
    /// in the compilation, the pipeline bails immediately with no output.
    /// </summary>
    /// <remarks>
    /// Current assumptions:
    /// <list type="bullet">
    /// <item>Queue/entity name is the first constructor argument of the trigger attribute.</item>
    /// <item>Connection name is a named property on the trigger attribute, identified by <see cref="ConnectionPropertyName"/>.</item>
    /// <item>Trigger method signatures are validated against <see cref="TriggerShape"/>.</item>
    /// </list>
    /// </remarks>
    internal sealed record TriggerDefinition(
        string TriggerAttributeMetadataName,
        ImmutableEquatableArray<AdditionalParameterType> AdditionalParameterTypes,
        string ProcessorTypeFullyQualified,
        string ConnectionPropertyName,
        string ProcessMethodName,
        TriggerShape Shape);

    internal readonly record struct AdditionalParameterType(string MetadataName, ParameterRole Role) : IEquatable<AdditionalParameterType>;

    internal readonly record struct TriggerShape(ImmutableEquatableArray<ParameterRole> OrderedParameters, bool AllowAdditionalParameters)
    {
        public static TriggerShape Required(params ParameterRole[] orderedParameters)
            => new(orderedParameters.ToImmutableEquatableArray(), AllowAdditionalParameters: false);

        public static TriggerShape RequiredAllowingAdditionalParameters(params ParameterRole[] orderedParameters)
            => new(orderedParameters.ToImmutableEquatableArray(), AllowAdditionalParameters: true);
    }

    internal readonly record struct ParameterRole(string Name) : IEquatable<ParameterRole>
    {
        public static readonly ParameterRole TriggerMessage = new("TriggerMessage");
        public static readonly ParameterRole FunctionContext = new("FunctionContext");
        public static readonly ParameterRole CancellationToken = new("CancellationToken");

        public override string ToString() => Name;
    }
}