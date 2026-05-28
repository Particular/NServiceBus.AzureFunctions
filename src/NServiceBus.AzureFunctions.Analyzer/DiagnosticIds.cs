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
    public const string PurgeOnStartupNotAllowed = "NSBFUNC008";
    public const string LimitMessageProcessingToNotAllowed = "NSBFUNC009";
    public const string DefineCriticalErrorActionNotAllowed = "NSBFUNC010";
    public const string SetDiagnosticsPathNotAllowed = "NSBFUNC011";
    public const string MakeInstanceUniquelyAddressableNotAllowed = "NSBFUNC012";
    public const string OverrideLocalAddressNotAllowed = "NSBFUNC013";
    public const string RouteReplyToThisInstanceNotAllowed = "NSBFUNC014";
    public const string RouteToThisInstanceNotAllowed = "NSBFUNC015";
    public const string UseTransportRequiresAzureServiceBusServerlessTransport = "NSBFUNC016";

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

    internal static readonly DiagnosticDescriptor PurgeOnStartupNotAllowedDescriptor = new(
        id: PurgeOnStartupNotAllowed,
        title: "PurgeOnStartup is not supported in Azure Functions",
        messageFormat: "Azure Functions endpoints do not support PurgeOnStartup",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LimitMessageProcessingToNotAllowedDescriptor = new(
        id: LimitMessageProcessingToNotAllowed,
        title: "LimitMessageProcessingConcurrencyTo is not supported in Azure Functions",
        messageFormat: "Concurrency-related settings are controlled via the Azure Functions host configuration",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DefineCriticalErrorActionNotAllowedDescriptor = new(
        id: DefineCriticalErrorActionNotAllowed,
        title: "DefineCriticalErrorAction is not supported in Azure Functions",
        messageFormat: "Azure Functions endpoints do not control the application lifecycle and should not define behavior for critical errors",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor SetDiagnosticsPathNotAllowedDescriptor = new(
        id: SetDiagnosticsPathNotAllowed,
        title: "SetDiagnosticsPath is not supported in Azure Functions",
        messageFormat: "Azure Functions endpoints should not write diagnostics to the local file system. Use CustomDiagnosticsWriter to write diagnostics elsewhere.",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MakeInstanceUniquelyAddressableNotAllowedDescriptor = new(
        id: MakeInstanceUniquelyAddressableNotAllowed,
        title: "Unique instance addressing is not supported in Azure Functions",
        messageFormat: "Azure Functions endpoints have unpredictable lifecycles and should not be uniquely addressable",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor OverrideLocalAddressNotAllowedDescriptor = new(
        id: OverrideLocalAddressNotAllowed,
        title: "OverrideLocalAddress is not supported in Azure Functions",
        messageFormat: "The NServiceBus endpoint address in Azure Functions is determined by the function trigger configuration",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor RouteReplyToThisInstanceNotAllowedDescriptor = new(
        id: RouteReplyToThisInstanceNotAllowed,
        title: "RouteReplyToThisInstance is not supported in Azure Functions",
        messageFormat: "Azure Functions instances cannot be directly addressed because they have a volatile lifetime. Use endpoint routing instead.",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor RouteToThisInstanceNotAllowedDescriptor = new(
        id: RouteToThisInstanceNotAllowed,
        title: "RouteToThisInstance is not supported in Azure Functions",
        messageFormat: "Azure Functions instances cannot be directly addressed because they have a volatile lifetime. Use endpoint routing instead.",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UseTransportRequiresAzureServiceBusServerlessTransportDescriptor = new(
        id: UseTransportRequiresAzureServiceBusServerlessTransport,
        title: "UseTransport must use AzureServiceBusServerlessTransport in Azure Functions",
        messageFormat: "Azure Functions endpoints must be configured with AzureServiceBusServerlessTransport when calling EndpointConfiguration.UseTransport(...)",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
