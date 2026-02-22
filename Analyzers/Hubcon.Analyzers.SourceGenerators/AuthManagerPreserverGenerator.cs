using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HubconAnalyzers.SourceGenerators
{
    [Generator]
    public class AuthManagerPreserverGenerator : IIncrementalGenerator
    {
        private const string TargetBaseType = "Hubcon.BaseAuthenticationManager";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. Buscador de "Disparador" más robusto
            //var hasCallToInitializer = context.SyntaxProvider
            //    .CreateSyntaxProvider(
            //        predicate: (s, _) => s is InvocationExpressionSyntax, // Miramos todas las invocaciones
            //        transform: (ctx, _) =>
            //        {
            //            var invocation = (InvocationExpressionSyntax)ctx.Node;

            //            // Filtro rápido por nombre antes de pedir el modelo semántico (performance)
            //            var methodName = "";

            //            if (invocation.Expression is MemberAccessExpressionSyntax m)
            //                methodName = m.Name.Identifier.Text;
            //            else if (invocation.Expression is IdentifierNameSyntax i)
            //                methodName = i.Identifier.Text;
            //            else 
            //                methodName = null;

            //            if (methodName != "AddHubconClient") return false;

            //            // Confirmación semántica: ¿Es realmente NUESTRO método?
            //            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            //            if (symbol == null) return false;

            //            // Verificamos que el método pertenezca a tus namespaces de Hubcon
            //            return symbol.ContainingNamespace.ToDisplayString().StartsWith("Hubcon");
            //        })
            //    .Where(isHit => isHit) // Solo nos quedamos con los "true"
            //    .Collect()
            //    .Select((calls, _) => calls.Any());

            // 1. Buscador del "Trigger" específico
            var hasCallToInitializer = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => node is InvocationExpressionSyntax,
                    transform: (ctx, _) =>
                    {
                        var invocation = (InvocationExpressionSyntax)ctx.Node;

                        // 1a. Filtro rápido por nombre (No gasta CPU)
                        var name = "";

                        if (invocation.Expression is MemberAccessExpressionSyntax m)
                            name = m.Name.Identifier.Text;
                        else if (invocation.Expression is IdentifierNameSyntax i)
                            name = i.Identifier.Text;
                        else
                            name = null;


                        if (name != "AddHubconClient") return false;

                        // 1b. Validación Semántica (La verdad absoluta)
                        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (symbol == null) return false;

                        // Verificamos: Namespace == "Hubcon" && Clase == "DependencyInjection"
                        return symbol.ContainingType?.Name == "DependencyInjection" &&
                               symbol.ContainingNamespace?.ToDisplayString() == "Hubcon";
                    })
                .Where(found => found)
                .Collect()
                .Select((calls, _) => calls.Any());


            // 1. Filtrado sintáctico
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is ClassDeclarationSyntax && ((ClassDeclarationSyntax)s).BaseList != null,
                    transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(m => m != null);

            // 2. Análisis semántico
            var compilationAndClasses = context.CompilationProvider.
                Combine(classDeclarations.Collect());

            IncrementalValuesProvider<INamedTypeSymbol> authManagers = compilationAndClasses
                .SelectMany((pair, _) =>
                {
                    var results = new List<INamedTypeSymbol>();

                    var compilation = pair.Left;
                    var classes = pair.Right;

                    var targetSymbol = compilation.GetTypeByMetadataName(TargetBaseType);
                    if (targetSymbol == null) return results;

                    foreach (var classSyntax in classes)
                    {
                        var model = compilation.GetSemanticModel(classSyntax.SyntaxTree);
                        var classSymbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;

                        if (classSymbol != null && InheritsFrom(classSymbol, targetSymbol))
                        {
                            results.Add(classSymbol);
                        }
                    }
                    return results;
                });


            var finalProvider = authManagers.
                Combine(context.CompilationProvider.Select((c, _) => c.AssemblyName))
                .Combine(hasCallToInitializer);

            // 3. Generación del código
            context.RegisterSourceOutput(finalProvider, (spc, data) =>
            {
                var ((symbol, assemblyName), shouldGenerate) = data;

                if (!shouldGenerate)
                    return;

                if (assemblyName == "Hubcon.Client")
                    return;

                string source = GeneratePreserverSource(symbol);
                spc.AddSource(symbol.Name + "_AuthPreserver.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
        {
            var current = symbol.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
                current = current.BaseType;
            }
            return false;
        }

        private static string GeneratePreserverSource(INamedTypeSymbol symbol)
        {
            string fullTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string className = symbol.Name;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class " + className + "AuthPreserver");
            sb.AppendLine("    {");
            sb.AppendLine("        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine("        public static void Init()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Environment.TickCount < 0)");
            sb.AppendLine("            {");

            // Preservar constructores
            int i = 0;
            foreach (var ctor in symbol.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
            {
                i++;
                var paramTypes = ctor.Parameters.Select(p => "(" + p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")default");
                string paramsList = string.Join(", ", paramTypes);
                sb.AppendLine($"                var instance{i} = new " + fullTypeName + "(" + paramsList + ");");

                // Preservar métodos
                foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public))
                {
                    var methodParamTypes = method.Parameters.Select(p => "(" + p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")default");
                    string methodParamsList = string.Join(", ", methodParamTypes);
                    sb.AppendLine("                instance." + method.Name + "(" + methodParamsList + ");");
                }
            }


            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(" + fullTypeName + "))]");
            sb.AppendLine("        public static void Preserve() { }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
