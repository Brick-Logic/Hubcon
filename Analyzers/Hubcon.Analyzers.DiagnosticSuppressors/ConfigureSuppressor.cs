using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MinimalSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor SuppressDescriptor =
            new SuppressionDescriptor(
                id: "SUP8974",
                suppressedDiagnosticId: "CS8974", // warning que queremos suprimir
                justification: "Using 'Expression<Func<T, object>>' in Configure method from IOperationSelector<T> is an intended behaviour."
            );

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(SuppressDescriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        // No hace nada
    }
}
namespace Hubcon.Analyzers.DiagnosticSuppressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigureSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor SuppressDescriptor =
            new SuppressionDescriptor(
                id: "SUP8974",
                suppressedDiagnosticId: "CS8974", // warning que queremos suprimir
                justification: "Using 'Expression<Func<T, object>>' in Configure method from IOperationSelector<T> is an intended behaviour."
            );

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
            ImmutableArray.Create(SuppressDescriptor);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                // Nos interesa suprimir solo si ocurre en la interfaz específica
                var location = diagnostic.Location;
                var semanticModel = context.GetSemanticModel(location.SourceTree);
                var root = location.SourceTree.GetRoot(context.CancellationToken);

                var node = root.FindNode(location.SourceSpan);

                if (node is null)
                    continue;

                var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken) ??
                             semanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;

                if (symbol is IMethodSymbol method &&
                    method.Name == "Configure" &&
                    method.ContainingType is INamedTypeSymbol type &&
                    type.Name.Contains("IOperationSelector<") &&
                    type.TypeParameters.Length == 1)
                {
                    // Solo suprimir CS8974 para Configure en esa interfaz
                    context.ReportSuppression(Suppression.Create(SuppressDescriptor, diagnostic));
                }
            }
        }
    }
}
