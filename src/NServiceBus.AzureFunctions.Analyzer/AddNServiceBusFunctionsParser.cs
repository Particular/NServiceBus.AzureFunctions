namespace NServiceBus.AzureFunctions.Analyzer;

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Utility;

static class AddNServiceBusFunctionsParser
{
    public static bool SyntaxLooksLikeInvocation(SyntaxNode node) => node is InvocationExpressionSyntax
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

    public static AddNServiceBusFunctionsInvocationSpec? ParseInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellationToken = default)
    {
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
        {
            return null;
        }

        // Make sure the method we're looking at is ours and not some (extremely unlikely) copycat.
        if (!IsAddNServiceBusFunctionsMethod(operation.TargetMethod))
        {
            return null;
        }

        if (semanticModel.GetInterceptableLocation(invocation, cancellationToken) is not { } location)
        {
            return null;
        }

        return new AddNServiceBusFunctionsInvocationSpec(InterceptLocationSpec.From(location));
    }

    static bool IsAddNServiceBusFunctionsMethod(IMethodSymbol method) =>
        (method.ReducedFrom ?? method) is
        {
            Name: AddNServiceBusFunctionsMethodName,
            IsGenericMethod: false,
            IsExtensionMethod: true,
            ContainingType:
            {
                Name: AddNServiceBusFunctionsClassName,
                ContainingNamespace:
                {
                    Name: "NServiceBus",
                    ContainingNamespace.IsGlobalNamespace: true
                }
            },
            Parameters:
            [
                {
                    Type: INamedTypeSymbol
                    {
                        Name: "FunctionsApplicationBuilder",
                        ContainingNamespace:
                        {
                            Name: "Builder",
                            ContainingNamespace:
                            {
                                Name: "Worker",
                                ContainingNamespace:
                                {
                                    Name: "Functions",
                                    ContainingNamespace:
                                    {
                                        Name: "Azure",
                                        ContainingNamespace:
                                        {
                                            Name: "Microsoft",
                                            ContainingNamespace.IsGlobalNamespace: true
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            ]
        };

    const string AddNServiceBusFunctionsMethodName = "AddNServiceBusFunctions";
    const string AddNServiceBusFunctionsClassName = "HostApplicationBuilderExtensions";
}

readonly record struct AddNServiceBusFunctionsInvocationSpec(InterceptLocationSpec LocationSpec);