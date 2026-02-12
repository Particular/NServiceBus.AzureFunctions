namespace NServiceBus.AzureFunctions.CodeFixes
{
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Editing;
    using Microsoft.CodeAnalysis.Formatting;

    [Shared]
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddConfigureMethodFixer))]
    public class AddConfigureMethodFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("NSBFUNC001");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root?.FindNode(context.Span) is not ClassDeclarationSyntax classDecl)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add Configure(EndpointConfiguration) method",
                    cancellationToken => AddConfigureMethod(context.Document, classDecl, cancellationToken),
                    EquivalenceKey),
                diagnostic);
        }

        static async Task<Document> AddConfigureMethod(
            Document document,
            ClassDeclarationSyntax classDecl,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var configureMethod = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
@"    public static void Configure(EndpointConfiguration endpoint)
    {
        throw new System.NotImplementedException();
    }
")!;

            var newClassDecl = classDecl.AddMembers(configureMethod.WithAdditionalAnnotations(Formatter.Annotation));

            editor.ReplaceNode(classDecl, newClassDecl);

            return editor.GetChangedDocument();
        }

        static readonly string EquivalenceKey = typeof(AddConfigureMethodFixer).FullName;
    }
}