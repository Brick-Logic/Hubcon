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
    public class SubscriptionMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HCN0004";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "ISubscription not allowed in controller methods",
            messageFormat: "The method '{0}' returns ISubscription or ISubscription<T>, which is only allowed on properties.",
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
            if (IsSubscriptionType(returnType))
            {
                var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsSubscriptionType(ITypeSymbol type)
        {
            if (type == null) return false;
            return type.Name == "ISubscription";
        }

        private bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
        {
            return type.AllInterfaces.Any(i => i.Name == interfaceName);
        }
    }
}
