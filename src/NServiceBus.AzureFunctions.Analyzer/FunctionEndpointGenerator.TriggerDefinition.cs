namespace NServiceBus.AzureFunctions.Analyzer;

using System;
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
    /// Trigger-specific parsing behavior is configured via policy types:
    /// <list type="bullet">
    /// <item>Address/entity name extraction via <see cref="AddressExtractionPolicy"/>.</item>
    /// <item>Connection setting extraction via <see cref="ConnectionSettingPolicy"/>.</item>
    /// <item>Auto-complete validation via <see cref="AutoCompletePolicy"/>.</item>
    /// <item>Trigger method signatures are validated against <see cref="TriggerShape"/>.</item>
    /// </list>
    /// </remarks>
    internal sealed record TriggerDefinition(
        string TriggerAttributeMetadataName,
        ImmutableEquatableArray<AdditionalParameterType> AdditionalParameterTypes,
        string ProcessorTypeFullyQualified,
        AddressExtractionPolicy AddressExtraction,
        ConnectionSettingPolicy ConnectionSetting,
        AutoCompletePolicy AutoComplete,
        string RegistrationMethodFullyQualified,
        string ProcessMethodName,
        TriggerShape Shape);

    internal abstract record AddressExtractionPolicy
    {
        internal sealed record ConstructorArgument(int Index) : AddressExtractionPolicy;
        internal sealed record ConstructorParameterNamed(string ParameterName) : AddressExtractionPolicy;
        internal sealed record NamedProperty(string PropertyName) : AddressExtractionPolicy;

        public static AddressExtractionPolicy FromConstructorArgument(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
            }

            return new ConstructorArgument(index);
        }

        public static AddressExtractionPolicy FromNamedProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
            }

            return new NamedProperty(propertyName);
        }

        public static AddressExtractionPolicy FromNamedConstructorParameter(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name cannot be null or whitespace.", nameof(parameterName));
            }

            return new ConstructorParameterNamed(parameterName);
        }
    }

    internal abstract record ConnectionSettingPolicy
    {
        internal sealed record None : ConnectionSettingPolicy;
        internal sealed record NamedProperty(string PropertyName) : ConnectionSettingPolicy;

        public static ConnectionSettingPolicy NotConfigured { get; } = new None();

        public static ConnectionSettingPolicy FromNamedProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
            }

            return new NamedProperty(propertyName);
        }
    }

    internal abstract record AutoCompletePolicy
    {
        internal sealed record NotApplicable : AutoCompletePolicy;
        internal sealed record MustBeFalse(string PropertyName) : AutoCompletePolicy;

        public static AutoCompletePolicy None { get; } = new NotApplicable();

        public static AutoCompletePolicy MustBeFalseFor(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
            }

            return new MustBeFalse(propertyName);
        }
    }

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
