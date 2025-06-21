using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace HubconAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncControllerMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HCN0001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Sync method on a hubcon controller",
            messageFormat: "The method '{0}' does not return Task or Task<T>. If the client uses hubcon automatic integration, the method invocation could block the client's main thread.",
            category: "Hubcon",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var methodSymbol = (IMethodSymbol)context.Symbol;

            // Ignorar constructores y propiedades
            if (methodSymbol.MethodKind != MethodKind.Ordinary)
                return;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return;

            // Verifica si implementa IControllerContract o derivados
            var implementsIController = ImplementsInterface(containingType, nameof(IControllerContract));
            if (!implementsIController)
                return;

            // Verifica si retorna Task o Task<T>
            var returnType = methodSymbol.ReturnType;
            if (!IsTaskType(returnType))
            {
                var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsTaskType(ITypeSymbol type)
        {
            return type.Name == "Task" &&
                   type.ContainingNamespace.ToDisplayString().StartsWith("System.Threading.Tasks");
        }

        private bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
        {
            return type.AllInterfaces.Any(i => i.Name == interfaceName);
        }
    }
}