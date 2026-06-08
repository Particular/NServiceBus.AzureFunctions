namespace NServiceBus.AzureFunctions.Analyzer;

using System.Threading;
using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Utility;

public sealed partial class FunctionCompositionInterceptor
{
    internal readonly record struct InterceptableCompositionSpec(InterceptLocationSpec LocationSpec, string? RootNamespace);

    internal readonly record struct InterceptableCompositionSpecs(ImmutableEquatableArray<InterceptableCompositionSpec> Specs);

    internal static class Parser
    {
        public static bool SyntaxLooksLikeAddNServiceBusFunctionsMethod(SyntaxNode node) => node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name: IdentifierNameSyntax
                {
                    Identifier.ValueText: AddNServiceBusFunctionsMethodName
                },
            },
            ArgumentList.Arguments.Count: 0,
        };

        static bool IsAddNServiceBusFunctionsMethod(IMethodSymbol method) => method is
        {
            Name: AddNServiceBusFunctionsMethodName,
            IsGenericMethod: false,
            Parameters.Length: 1,
            ContainingType:
            {
                Name: AddNServiceBusFunctionsClassName,
                ContainingNamespace:
                {
                    Name: "NServiceBus",
                    ContainingNamespace.IsGlobalNamespace: true
                }
            }
        };

        public static InterceptableCompositionSpec? Parse(InvocationExpressionSyntax invocation, SemanticModel semanticModel, HostProjectSpec hostProject, CancellationToken cancellationToken = default)
        {
            if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
            {
                return null;
            }

            // Make sure the method we're looking at is ours and not some (extremely unlikely) copycat
            if (!IsAddNServiceBusFunctionsMethod(operation.TargetMethod))
            {
                return null;
            }

            if (semanticModel.GetInterceptableLocation(invocation, cancellationToken) is not { } location)
            {
                return null;
            }

            return new InterceptableCompositionSpec(InterceptLocationSpec.From(location), hostProject.RootNamespace);
        }

        const string AddNServiceBusFunctionsMethodName = "AddNServiceBusFunctions";
        const string AddNServiceBusFunctionsClassName = "HostApplicationBuilderExtensions";
    }
}