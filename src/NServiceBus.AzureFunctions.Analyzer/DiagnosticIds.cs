#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

static class DiagnosticIds
{
    public const string ClassMustBePartial = "NSBFUNC001";
    public const string ShouldNotImplementIHandleMessages = "NSBFUNC002";
    public const string MethodMustBePartial = "NSBFUNC003";
    public const string MissingAddNServiceBusFunctionsCall = "NSBFUNC004";

    public static readonly DiagnosticDescriptor ClassMustBePartialDescriptor = new(
        id: ClassMustBePartial,
        title: "Class containing [NServiceBusFunction] must be partial",
        messageFormat: "Class '{0}' must be declared as partial to use [NServiceBusFunction]",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ShouldNotImplementIHandleMessagesDescriptor = new(
        id: ShouldNotImplementIHandleMessages,
        title: "Function class should not implement IHandleMessages<T>",
        messageFormat: "Class '{0}' should not implement IHandleMessages<T>; message handlers should be registered separately via IEndpointConfiguration",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustBePartialDescriptor = new(
        id: MethodMustBePartial,
        title: "Method with [NServiceBusFunction] must be partial",
        messageFormat: "Method '{0}' must be declared as partial to use [NServiceBusFunction]",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingAddNServiceBusFunctionsCallDescriptor = new(
        id: MissingAddNServiceBusFunctionsCall,
        title: "AddNServiceBusFunctions() is not called",
        messageFormat: "This project has NServiceBus endpoint registrations but does not call builder.AddNServiceBusFunctions(). Endpoints will not be started.",
        category: "NServiceBus.AzureFunctions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}