using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExtractConstArrayAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExtractConstArrayAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ExtractConstArrayAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeArrayCreationExpression, SyntaxKind.ArrayInitializerExpression);
        }

        static bool IsConstant(ExpressionSyntax syntax, SyntaxNodeAnalysisContext context)
        {
            return context.SemanticModel.GetSymbolInfo(syntax).Symbol is IFieldSymbol info && info.IsConst;
        }

        private static void AnalyzeArrayCreationExpression(SyntaxNodeAnalysisContext context)
        {
            var arrayCreationExpression = context.Node as InitializerExpressionSyntax; ;
            if (!(arrayCreationExpression.Parent.Parent is ArgumentSyntax))
                return;
            if (!arrayCreationExpression.Expressions.All(x => x is LiteralExpressionSyntax || IsConstant(x, context)))
                return;
            var diagnostic = Diagnostic.Create(Rule, arrayCreationExpression.Parent.Parent.GetLocation(), arrayCreationExpression.Parent.Parent);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
