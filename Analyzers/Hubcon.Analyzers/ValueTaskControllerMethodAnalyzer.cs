using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Hubcon.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ValueTaskControllerMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HCN0002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "ValueTask is not supported in controller methods",
            messageFormat: "The method '{0}' returns ValueTask or ValueTask<T>, which is not supported in Hubcon controllers.",
            category: "Hubcon",
            DiagnosticSeverity.Error,
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

            if (methodSymbol.MethodKind != MethodKind.Ordinary)
                return;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return;

            if (!Tools.InControllerScope(containingType))
                return;

            var returnType = methodSymbol.ReturnType;
            if (IsValueTaskType(returnType))
            {
                var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsValueTaskType(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.Name == "ValueTask" && type.ContainingNamespace.ToDisplayString().StartsWith("System.Threading.Tasks"))
                return true;
            return false;
        }

        private bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
        {
            return type.AllInterfaces.Any(i => i.Name == interfaceName);
        }
    }

}
