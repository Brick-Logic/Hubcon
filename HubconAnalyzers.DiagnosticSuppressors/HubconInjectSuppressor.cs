using Hubcon.Shared.Abstractions.Standard.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace HubconAnalyzers.DiagnosticSuppressors
{
    /// <summary>
    /// Suprime CS8618 para cualquier propiedad con [HubconInject].
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class HubconInjectSuppressor : DiagnosticSuppressor
    {
        // Identificador propio de la supresión
        private const string SuppressionId = "HUBSUPP0001";
        private static readonly SuppressionDescriptor Rule = new SuppressionDescriptor(
            id: SuppressionId,
            suppressedDiagnosticId: "CS8618",
            justification: "La propiedad será inyectada en tiempo de ejecución por HubconInject"
        );

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions
            => ImmutableArray.Create(Rule);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diag in context.ReportedDiagnostics)
            {
                // Solo nos interesa CS8618
                if (diag.Id != "CS8618")
                    continue;

                var tree = diag.Location.SourceTree;
                if (tree == null)
                    continue;

                var root = tree.GetRoot(context.CancellationToken);
                // Buscamos la PropertyDeclaration donde ocurrió CS8618
                var token = root.FindToken(diag.Location.SourceSpan.Start);
                var prop = token.Parent
                                .AncestorsAndSelf()
                                .OfType<PropertyDeclarationSyntax>()
                                .FirstOrDefault();
                if (prop == null)
                    continue;

                // Verificamos que tenga [HubconInject]
                var model = context.GetSemanticModel(tree);
                var sym = model.GetDeclaredSymbol(prop, context.CancellationToken);
                if (sym == null)
                    continue;

                var hasAttr = sym.GetAttributes()
                                 .Any(a => a.AttributeClass?.Name == nameof(HubconInjectAttribute));
                if (!hasAttr)
                    continue;

                context.ReportSuppression(Suppression.Create(Rule, diag));
            }
        }
    }
}
