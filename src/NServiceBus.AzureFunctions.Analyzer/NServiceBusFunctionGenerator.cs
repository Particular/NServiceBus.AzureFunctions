namespace NServiceBus.AzureFunctions.Analyzers
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [Generator]
    public class NServiceBusFunctionGenerator : IIncrementalGenerator
    {
        static readonly DiagnosticDescriptor MissingConfigureMethod = new(
            id: "NSBFUNC001",
            title: "NServiceBus endpoint must have a Configure method",
            messageFormat: "Class '{0}' is decorated with [NServiceBusEndpoint] but has no Configure(EndpointConfiguration) method. Add a Configure method or inherit from a base class that provides one.",
            category: "NServiceBus.AzureFunctions",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        static readonly DiagnosticDescriptor EmptyConfigureMethod = new(
            id: "NSBFUNC002",
            title: "NServiceBus endpoint Configure method must register at least one handler",
            messageFormat: "Class '{0}' Configure method does not register any handlers. Add at least one AddHandler<T>() call or call base.Configure() to configure the endpoint.",
            category: "NServiceBus.AzureFunctions",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        static readonly DiagnosticDescriptor DuplicateHandler = new(
            id: "NSBFUNC003",
            title: "Handler type registered in multiple endpoints",
            messageFormat: "Handler type '{0}' is registered in multiple endpoints: {1}. A handler can only belong to one endpoint.",
            category: "NServiceBus.AzureFunctions",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpoints = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "NServiceBus.NServiceBusEndpointAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => GetEndpointInfo(ctx))
                .Where(static info => info != null);

            var collected = endpoints.Collect();

            context.RegisterSourceOutput(collected, static (spc, eps) => Execute(spc, eps));
        }

        static EndpointInfo? GetEndpointInfo(GeneratorAttributeSyntaxContext context)
        {
            var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
            var attribute = context.Attributes[0];

            string? endpointName = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                !attribute.ConstructorArguments[0].IsNull)
            {
                endpointName = attribute.ConstructorArguments[0].Value as string;
            }

            if (string.IsNullOrEmpty(endpointName))
            {
                endpointName = classSymbol.Name;
            }

            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : classSymbol.ContainingNamespace.ToDisplayString();

            var configureMethod = FindConfigureMethod(classSymbol);
            var configureKind = ConfigureMethodKind.None;
            if (configureMethod != null)
            {
                configureKind = configureMethod.IsStatic
                    ? ConfigureMethodKind.Static
                    : ConfigureMethodKind.Instance;
            }

            var configureBodyKind = ConfigureBodyKind.Unknown;
            var handlerTypes = ImmutableArray<string>.Empty;
            if (configureMethod != null &&
                SymbolEqualityComparer.Default.Equals(configureMethod.ContainingType, classSymbol))
            {
                var classDecl = (ClassDeclarationSyntax)context.TargetNode;
                var methodSyntax = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "Configure");

                if (methodSyntax != null)
                {
                    configureBodyKind = InspectConfigureBody(methodSyntax);
                    handlerTypes = ExtractHandlerTypes(methodSyntax, context.SemanticModel);
                }
            }

            var location = context.TargetNode.GetLocation();

            return new EndpointInfo(classSymbol.Name, namespaceName, endpointName!, configureKind, configureBodyKind, handlerTypes, location);
        }

        static IMethodSymbol? FindConfigureMethod(INamedTypeSymbol classSymbol)
        {
            var current = classSymbol;
            while (current != null)
            {
                var method = current.GetMembers("Configure")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m.ReturnsVoid &&
                        m.Parameters.Length == 1 &&
                        m.Parameters[0].Type.ToDisplayString() == "NServiceBus.EndpointConfiguration");

                if (method != null)
                {
                    return method;
                }

                current = current.BaseType;
            }

            return null;
        }

        static ConfigureBodyKind InspectConfigureBody(MethodDeclarationSyntax method)
        {
            var body = method.Body;
            if (body == null)
            {
                if (method.ExpressionBody != null)
                {
                    return HasMeaningfulCall(method.ExpressionBody)
                        ? ConfigureBodyKind.Configured
                        : ConfigureBodyKind.Stub;
                }

                return ConfigureBodyKind.Stub;
            }

            if (body.Statements.Count == 0)
            {
                return ConfigureBodyKind.Stub;
            }

            if (body.Statements.Count == 1 && body.Statements[0] is ThrowStatementSyntax)
            {
                return ConfigureBodyKind.Stub;
            }

            return HasMeaningfulCall(body)
                ? ConfigureBodyKind.Configured
                : ConfigureBodyKind.Stub;
        }

        static bool HasMeaningfulCall(SyntaxNode node)
        {
            foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Expression is BaseExpressionSyntax &&
                        memberAccess.Name.Identifier.Text == "Configure")
                    {
                        return true;
                    }

                    if (memberAccess.Name is GenericNameSyntax genericName &&
                        genericName.Identifier.Text.StartsWith("Add"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static ImmutableArray<string> ExtractHandlerTypes(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            SyntaxNode? searchNode = method.Body ?? (SyntaxNode?)method.ExpressionBody;
            if (searchNode == null)
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>();

            foreach (var invocation in searchNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name is GenericNameSyntax genericName &&
                    genericName.Identifier.Text == "AddHandler" &&
                    genericName.TypeArgumentList.Arguments.Count == 1)
                {
                    var typeArg = genericName.TypeArgumentList.Arguments[0];
                    var typeInfo = semanticModel.GetTypeInfo(typeArg);
                    if (typeInfo.Type is INamedTypeSymbol handlerType)
                    {
                        builder.Add(handlerType.ToDisplayString());
                    }
                }
            }

            return builder.ToImmutable();
        }

        static void Execute(SourceProductionContext context, ImmutableArray<EndpointInfo?> endpoints)
        {
            var all = endpoints.Where(e => e != null).Select(e => e!).ToArray();
            if (all.Length == 0)
            {
                return;
            }

            foreach (var endpoint in all.Where(e => e.ConfigureKind == ConfigureMethodKind.None))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingConfigureMethod,
                    endpoint.Location,
                    endpoint.ClassName));
            }

            foreach (var endpoint in all.Where(e => e.ConfigureKind != ConfigureMethodKind.None && e.BodyKind == ConfigureBodyKind.Stub))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    EmptyConfigureMethod,
                    endpoint.Location,
                    endpoint.ClassName));
            }

            var valid = all.Where(e => e.ConfigureKind != ConfigureMethodKind.None).ToArray();
            if (valid.Length == 0)
            {
                return;
            }

            var duplicateHandlers = valid
                .SelectMany(e => e.HandlerTypes.Select(h => new { Handler = h, Endpoint = e }))
                .GroupBy(x => x.Handler)
                .Where(g => g.Select(x => x.Endpoint.EndpointName).Distinct().Count() > 1)
                .ToArray();

            foreach (var group in duplicateHandlers)
            {
                var endpointNames = string.Join(", ", group.Select(x => x.Endpoint.EndpointName).Distinct());
                foreach (var endpoint in group.Select(x => x.Endpoint).Distinct())
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateHandler,
                        endpoint.Location,
                        group.Key,
                        endpointNames));
                }
            }

            foreach (var endpoint in valid)
            {
                context.AddSource(
                    $"{endpoint.ClassName}.g.cs",
                    GenerateFunctionStub(endpoint));
            }

            context.AddSource(
                "NServiceBusFunctionsRegistration.g.cs",
                GenerateRegistration(valid));
        }

        static string GenerateFunctionStub(EndpointInfo endpoint)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");

            var indent = "";
            if (endpoint.Namespace != null)
            {
                sb.AppendLine($"namespace {endpoint.Namespace}");
                sb.AppendLine("{");
                indent = "    ";
            }

            sb.AppendLine($"{indent}using System.Threading;");
            sb.AppendLine($"{indent}using System.Threading.Tasks;");
            sb.AppendLine($"{indent}using Azure.Messaging.ServiceBus;");
            sb.AppendLine($"{indent}using Microsoft.Azure.Functions.Worker;");
            sb.AppendLine($"{indent}using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine($"{indent}using NServiceBus.AzureFunctions.AzureServiceBus;");
            sb.AppendLine();
            sb.AppendLine($"{indent}public partial class {endpoint.ClassName}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    [Function(\"{endpoint.EndpointName}\")]");
            sb.AppendLine($"{indent}    public Task ProcessMessage(");
            sb.AppendLine($"{indent}        [ServiceBusTrigger(\"{endpoint.EndpointName}\", Connection = \"AzureWebJobsServiceBus\", AutoCompleteMessages = true)]");
            sb.AppendLine($"{indent}        ServiceBusReceivedMessage message,");
            sb.AppendLine($"{indent}        FunctionContext functionContext,");
            sb.AppendLine($"{indent}        CancellationToken cancellationToken = default)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        var processor = functionContext.InstanceServices");
            sb.AppendLine($"{indent}            .GetRequiredKeyedService<IMessageProcessor>(\"{endpoint.EndpointName}\");");
            sb.AppendLine($"{indent}        return processor.Process(message, cancellationToken);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");

            if (endpoint.Namespace != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        static string GenerateRegistration(EndpointInfo[] endpoints)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("namespace NServiceBus");
            sb.AppendLine("{");
            sb.AppendLine("    using Microsoft.Azure.Functions.Worker.Builder;");
            sb.AppendLine("    using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("    using Microsoft.Extensions.Hosting;");
            sb.AppendLine();
            sb.AppendLine("    public static class NServiceBusFunctionsRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void UseNServiceBusFunctions(this FunctionsApplicationBuilder builder)");
            sb.AppendLine("        {");

            foreach (var endpoint in endpoints)
            {
                var fullName = endpoint.Namespace != null
                    ? $"{endpoint.Namespace}.{endpoint.ClassName}"
                    : endpoint.ClassName;

                if (endpoint.ConfigureKind == ConfigureMethodKind.Static)
                {
                    sb.AppendLine($"            builder.AddNServiceBusFunction(\"{endpoint.EndpointName}\", {fullName}.Configure);");
                }
                else
                {
                    sb.AppendLine($"            builder.AddNServiceBusFunction(\"{endpoint.EndpointName}\", new {fullName}().Configure);");
                }
            }

            sb.AppendLine();

            var names = string.Join(", ", endpoints.Select(e => $"\"{e.EndpointName}\""));
            sb.AppendLine($"            builder.Services.AddHostedService(sp => new FunctionConfigurationValidator(new[] {{ {names} }}, sp));");

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }

    enum ConfigureMethodKind
    {
        None,
        Static,
        Instance
    }

    enum ConfigureBodyKind
    {
        Unknown,
        Stub,
        Configured
    }

    sealed class EndpointInfo : IEquatable<EndpointInfo>
    {
        public string ClassName { get; }
        public string? Namespace { get; }
        public string EndpointName { get; }
        public ConfigureMethodKind ConfigureKind { get; }
        public ConfigureBodyKind BodyKind { get; }
        public ImmutableArray<string> HandlerTypes { get; }
        public Location? Location { get; }

        public EndpointInfo(string className, string? namespaceName, string endpointName, ConfigureMethodKind configureKind, ConfigureBodyKind bodyKind, ImmutableArray<string> handlerTypes, Location? location)
        {
            ClassName = className;
            Namespace = namespaceName;
            EndpointName = endpointName;
            ConfigureKind = configureKind;
            BodyKind = bodyKind;
            HandlerTypes = handlerTypes;
            Location = location;
        }

        public bool Equals(EndpointInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            return ClassName == other.ClassName
                && Namespace == other.Namespace
                && EndpointName == other.EndpointName
                && ConfigureKind == other.ConfigureKind
                && BodyKind == other.BodyKind
                && HandlerTypes.SequenceEqual(other.HandlerTypes);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as EndpointInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (ClassName?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Namespace?.GetHashCode() ?? 0);
                hash = (hash * 31) + (EndpointName?.GetHashCode() ?? 0);
                hash = (hash * 31) + ConfigureKind.GetHashCode();
                hash = (hash * 31) + BodyKind.GetHashCode();
                foreach (var h in HandlerTypes)
                {
                    hash = (hash * 31) + (h?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}