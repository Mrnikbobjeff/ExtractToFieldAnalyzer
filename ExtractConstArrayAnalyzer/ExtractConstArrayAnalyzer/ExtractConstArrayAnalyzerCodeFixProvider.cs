using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace ExtractConstArrayAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExtractConstArrayAnalyzerCodeFixProvider)), Shared]
    public class ExtractConstArrayAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Make uppercase";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ExtractConstArrayAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ArgumentSyntax>().First();

            var isInterface = declaration.FirstAncestorOrSelf<SyntaxNode>(x => x is StructDeclarationSyntax || x is ClassDeclarationSyntax) == declaration;
            if (isInterface)
                return;
            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Solution> MakeUppercaseAsync(Document document, ArgumentSyntax typeDecl, CancellationToken cancellationToken)
        {
            var originalSolution = document.Project.Solution;
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var paramsInvocation = typeDecl.FirstAncestorOrSelf<SyntaxNode>(x => x is InvocationExpressionSyntax) as InvocationExpressionSyntax;
            var method = semanticModel.GetSymbolInfo(paramsInvocation).Symbol as IMethodSymbol;
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            var bracketedSyntax = SyntaxFactory.BracketedArgumentList();
            var updatedParameters = new SeparatedSyntaxList<ExpressionSyntax>();
            var actualArguments = paramsInvocation.ArgumentList.Arguments.Skip(method.Parameters.Length - 1).Select(x => x.Expression).ToArray();
            updatedParameters = updatedParameters.AddRange(actualArguments);
            var newArray = SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, updatedParameters);
            var typeDisplayString = method.Parameters.Last().Type.ToMinimalDisplayString(semanticModel, method.Parameters.Last().Locations.First().SourceSpan.Start);

            var typeSyntax = SyntaxFactory.ParseTypeName(typeDisplayString);
            var objectCreationExpression = SyntaxFactory.ObjectCreationExpression(typeSyntax, null, newArray).WithAdditionalAnnotations(Formatter.Annotation);
            var equalsValueClause = SyntaxFactory.EqualsValueClause(objectCreationExpression);
            var declarator = new SeparatedSyntaxList<VariableDeclaratorSyntax>();
            declarator = declarator.Add(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("hoisted"), null, equalsValueClause));
            var variableAssignment = SyntaxFactory.VariableDeclaration(typeSyntax, declarator).WithAdditionalAnnotations(Formatter.Annotation);
            var assignmentExpression = SyntaxFactory.FieldDeclaration(
                new SyntaxList<AttributeListSyntax>(),
                new SyntaxTokenList().Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)).Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                variableAssignment);

            var invocationParameterReplacement = new SeparatedSyntaxList<ArgumentSyntax>();
            invocationParameterReplacement = invocationParameterReplacement.AddRange(paramsInvocation.ArgumentList.Arguments.TakeWhile(x => x != typeDecl));
            invocationParameterReplacement = invocationParameterReplacement.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("hoisted")));
            invocationParameterReplacement = invocationParameterReplacement.AddRange(paramsInvocation.ArgumentList.Arguments.Skip(invocationParameterReplacement.Count));
            var newArgListSyntax = SyntaxFactory.ArgumentList(invocationParameterReplacement);
            var newDeclaration = paramsInvocation.WithArgumentList(newArgListSyntax);

            var classOrStruct = typeDecl.FirstAncestorOrSelf<SyntaxNode>(x => x is StructDeclarationSyntax || x is ClassDeclarationSyntax);
            if(classOrStruct == typeDecl)
                context.
            var documentEditor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            documentEditor.InsertMembers(classOrStruct, 0, new[] { assignmentExpression });
            documentEditor.ReplaceNode(paramsInvocation, newDeclaration);

            var newDocument = documentEditor.GetChangedDocument();
            var finalRoot = await newDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            finalRoot = Formatter.Format(finalRoot, Formatter.Annotation, document.Project.Solution.Workspace);
            return originalSolution.WithDocumentSyntaxRoot(document.Id, finalRoot);
        }
    }
}
