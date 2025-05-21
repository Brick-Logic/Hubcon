using Microsoft.CodeAnalysis;
using System.Linq;

namespace HubconAnalyzer
{
    using Hubcon.Core.Abstractions.Standard.Attributes;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System.Collections.Immutable;

    namespace HubconAnalyzer
    {
        //[DiagnosticAnalyzer(LanguageNames.CSharp)]
        //public class HubconInjectNullableAnalyzer : DiagnosticAnalyzer
        //{
        //    public const string DiagnosticId = "HUBCON001";

        //    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        //        DiagnosticId,
        //        "Propiedad con HubconInject sin SuppressMessage",
        //        "Agregar SuppressMessage para suprimir el warning CS8618 en propiedades con [HubconInject]",
        //        "NullableWarning",
        //        DiagnosticSeverity.Warning,
        //        isEnabledByDefault: true);

        //    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        //    public override void Initialize(AnalysisContext context)
        //    {
        //        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        //        context.EnableConcurrentExecution();
        //        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        //    }

        //    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        //    {
        //        var property = (PropertyDeclarationSyntax)context.Node;

        //        // ¿Tiene el atributo [HubconInject]?
        //        var hasInject = property.AttributeLists
        //            .SelectMany(al => al.Attributes)
        //            .Any(attr => attr.Name.ToString().Contains(nameof(HubconInjectAttribute).Replace("Attribute", "")));

        //        if (!hasInject)
        //            return;

        //        // ¿Ya tiene un SuppressMessage para CS8618?
        //        var hasSuppress = property.AttributeLists
        //            .SelectMany(al => al.Attributes)
        //            .Any(attr =>
        //                attr.Name.ToString().Contains("SuppressMessage") &&
        //                attr.ArgumentList?.Arguments.Count == 2 &&
        //                attr.ArgumentList.Arguments[1].ToString().Contains("CS8618"));

        //        if (hasSuppress)
        //            return;

        //        var diagnostic = Diagnostic.Create(Rule, property.GetLocation());
        //        context.ReportDiagnostic(diagnostic);
        //    }
        //}
    }

}
