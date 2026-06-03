namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

#if !FIXES
public
#endif
static class DiagnosticIds
{
    public const string ClassMustBePartial = "NSBFUNC001";
    public const string ShouldNotImplementIHandleMessages = "NSBFUNC002";
    public const string MethodMustBePartial = "NSBFUNC003";
    public const string MissingAddNServiceBusFunctionsCall = "NSBFUNC004";
    public const string MultipleConfigureMethods = "NSBFUNC005";
    public const string AutoCompleteEnabled = "NSBFUNC006";
    public const string InvalidFunctionMethod = "NSBFUNC007";
    public const string InvalidEndpointConfiguration = "NSBFUNC008";
    public const string InvalidSendOptions = "NSBFUNC009";
    public const string InvalidEndpointTransportConfiguration = "NSBFUNC010";
    public const string InvalidSendOnlyEndpointMethod = "NSBFUNC011";

    internal static readonly DiagnosticDescriptor ClassMustBePartialDescriptor = new(
        id: ClassMustBePartial,
        title: "Class containing [NServiceBusFunction] must be partial",
        messageFormat: "Class '{0}' must be declared as partial to use [NServiceBusFunction]",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ShouldNotImplementIHandleMessagesDescriptor = new(
        id: ShouldNotImplementIHandleMessages,
        title: "Function class should not implement IHandleMessages<T>",
        messageFormat: "Class '{0}' should not implement IHandleMessages<T>; message handlers should be registered separately via IEndpointConfiguration",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MethodMustBePartialDescriptor = new(
        id: MethodMustBePartial,
        title: "Method with [NServiceBusFunction] must be partial",
        messageFormat: "Method '{0}' must be declared as partial to use [NServiceBusFunction]",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MissingAddNServiceBusFunctionsCallDescriptor = new(
        id: MissingAddNServiceBusFunctionsCall,
        title: "AddNServiceBusFunctions() is not called",
        messageFormat: "This project has NServiceBus endpoint registrations but does not call builder.AddNServiceBusFunctions(). Endpoints will not be started.",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    internal static readonly DiagnosticDescriptor MultipleConfigureMethodsDescriptor = new(
        id: MultipleConfigureMethods,
        title: "Multiple configuration methods found",
        messageFormat: "Multiple '{0}' configuration methods found on class '{1}'",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor AutoCompleteMustBeExplicitlyDisabled = new(
        id: AutoCompleteEnabled,
        title: "Message auto completion must be explicitly disabled",
        messageFormat: "The '{1}' property on [{2}] for method '{0}' must be explicitly set to false",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidFunctionMethodDescriptor = new(
        id: InvalidFunctionMethod,
        title: "Invalid NServiceBus function method",
        messageFormat: "Method '{0}' is not a valid NServiceBus function: {1}",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidEndpointConfigurationDescriptor = new(
        id: InvalidEndpointConfiguration,
        title: "Invalid endpoint configuration",
        messageFormat: "'{0}' is not supported for {1}: {2}",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidSendOptionsDescriptor = new(
        id: InvalidSendOptions,
        title: "Invalid send options",
        messageFormat: "'{0}' is not supported for {1}: {2}",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidEndpointTransportConfigurationDescriptor = new(
        id: InvalidEndpointTransportConfiguration,
        title: "Required transport configuration",
        messageFormat: "'{0}' must use AzureServiceBusServerlessTransport for {1}",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidSendOnlyEndpointMethodDescriptor = new(
        id: InvalidSendOnlyEndpointMethod,
        title: "Invalid NServiceBus send-only endpoint method",
        messageFormat: "Method '{0}' is not a valid NServiceBus send-only endpoint: {1}",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
