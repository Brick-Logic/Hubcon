using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Hubcon.Analyzers
{
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SubscriptionPropertyNullabilityAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HCN0003";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Subscription property should be nullable",
            messageFormat: "Property '{0}' is of type ISubscription or ISubscription<T> and should be nullable (use 'ISubscription?' or 'ISubscription<T>?').",
            category: "Hubcon",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        }

        private void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var propertySymbol = (IPropertySymbol)context.Symbol;

            var containingType = propertySymbol.ContainingType;
            if (containingType == null)
                return;

            if (!Tools.InControllerScope(containingType))
                return;

            // ¿Es ISubscription o ISubscription<T>?
            if (!IsSubscriptionType(propertySymbol.Type))
                return;

            // Advertir solo si NO es nullable (tratamos None como no-nullable también)
            var ann = propertySymbol.Type.NullableAnnotation;
            var isNullable = ann == NullableAnnotation.Annotated;

            if (!isNullable)
            {
                var location = propertySymbol.Locations.FirstOrDefault();
                if (location != null)
                {
                    var diagnostic = Diagnostic.Create(Rule, location, propertySymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsSubscriptionType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named)
            {
                // Mantiene la misma filosofía que tu código (comparar por nombre),
                // y cubre tanto ISubscription como ISubscription<T>
                if (named.Name == "ISubscription")
                    return true;

                // Por si en algún caso raro viene envuelto, revisamos definición original
                var def = named.OriginalDefinition;
                return def?.Name == "ISubscription";
            }
            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
        {
            return type.AllInterfaces.Any(i => i.Name == interfaceName);
        }
    }

}