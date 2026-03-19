namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

public static class DiagnosticIds
{
    public const string ClassMustBePartial = "NSBFUNC001";
    public const string ShouldNotImplementIHandleMessages = "NSBFUNC002";
    public const string MethodMustBePartial = "NSBFUNC003";
    public const string MissingAddNServiceBusFunctionsCall = "NSBFUNC004";
    public const string MultipleConfigureMethods = "NSBFUNC005";
    public const string AutoCompleteEnabled = "NSBFUNC006";

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
        messageFormat: "The auto complete property on the service bus trigger for method '{0}' must be explicitly set to false",
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
}