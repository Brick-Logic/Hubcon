using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Hubcon.Analyzers.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Hubcon.Analyzers.SourceGenerators.GeneratorCommands
{
    public static class GenerateServiceFactories
    {
        public static void Execute(ImmutableArray<INamedTypeSymbol> localSymbols,
            ImmutableArray<INamedTypeSymbol> referencedSymbols,
            ImmutableArray<INamedTypeSymbol> controllers,
            Compilation compilation,
            SourceProductionContext spc)
        {
            // Combinamos ambas listas de símbolos (locales + externos) en un único lote
            var allSeedSymbols = localSymbols.Concat(referencedSymbols).ToImmutableArray();
            
            var classesToPreserve = SymbolTools.ExpandPreservedSymbols(compilation, allSeedSymbols);

            if (classesToPreserve.Count == 0) return;

            var sbModules = new StringBuilder();
            var preserverCalls = new List<string>();

            var sbFinal = new StringBuilder();
            sbFinal.AppendLine("using System;");
            sbFinal.AppendLine("using System.Reflection;");
            sbFinal.AppendLine("using System.Runtime.CompilerServices;");
            sbFinal.AppendLine();
            sbFinal.AppendLine("namespace Hubcon.Generated.Preservers");
            sbFinal.AppendLine("{");

            foreach (var classSymbol in classesToPreserve)
            {
                var safePreserverName = classSymbol.GetSafeName();

                var result = GeneratePreserverForClass.Execute(classSymbol, safePreserverName, "    ");

                sbModules.AppendLine(result.code);
                sbModules.AppendLine();

                preserverCalls.Add("Hubcon.Generated.Preservers." + result.preserverMethod);
            }

            sbFinal.AppendLine(sbModules.ToString());

            sbFinal.AppendLine(
                "    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sbFinal.AppendLine("    public class HubconMasterPreserverInitializer");
            sbFinal.AppendLine("    {");


            foreach (var classSymbol in classesToPreserve)
            {
                sbFinal.AppendLine(
                    $"          [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({classSymbol.ToDisplayString()}))]");
                sbFinal.AppendLine(
                    $"          [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors, typeof({classSymbol.ToDisplayString()}))]");
            }

            sbFinal.AppendLine("        #if UNITY_2017_1_OR_NEWER");
            sbFinal.AppendLine(
                "        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sbFinal.AppendLine("        #else");
            sbFinal.AppendLine("        [ModuleInitializer]");
            sbFinal.AppendLine("        #endif");
            sbFinal.AppendLine("        public static void Init()");
            sbFinal.AppendLine("        {");
            sbFinal.AppendLine($"            {Tools.GetCondition()}");
            sbFinal.AppendLine("            {");

            for (int i = 0; i < preserverCalls.Count; i++)
            {
                sbFinal.AppendLine("                " + preserverCalls[i]);
            }

            sbFinal.AppendLine("            }");
            sbFinal.AppendLine("        }");
            sbFinal.AppendLine("    }");
            sbFinal.AppendLine("}");

            var classesToGenerateFactory = new List<INamedTypeSymbol>();
            classesToGenerateFactory.AddRange(controllers);
            classesToGenerateFactory.AddRange(classesToPreserve);

            spc.AddSource("HubconPreserverMetadata.g.cs", SourceText.From(sbFinal.ToString(), Encoding.UTF8));
            GenerateFactories(spc, classesToGenerateFactory, "HubconPreserversFactory.g.cs");
        }

        public static void GenerateFactories(SourceProductionContext spc, IEnumerable<INamedTypeSymbol> services, string fileName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Immutable;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class HubconServiceFactory");
            sb.AppendLine("    {");
            sb.AppendLine(
                "         private static IImmutableDictionary<Type, Func<IServiceProvider, object>> _factories;");
            sb.AppendLine();
            foreach (var item in services)
            {
                sb.AppendLine(
                    $"        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({item.ToDisplayString()}))]");
                sb.AppendLine(
                    $"        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors, typeof({item.ToDisplayString()}))]");
            }

            
            sb.AppendLine("        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine($"        public static void Init()");
            sb.AppendLine("        {");
            sb.AppendLine("             var factories = new Dictionary<Type, Func<IServiceProvider, object>>()");
            sb.AppendLine("             {");

            var controllersArray = services.ToArray();
            var count = controllersArray.Length;

            for (var i = 0; i < count; i++)
            {
                var currentService = controllersArray[i];
                var serviceTypeString = currentService.ToDisplayString();

                var constructors = currentService.InstanceConstructors;
                IMethodSymbol chosenConstructor = null;

                for (int c = 0; c < constructors.Length; c++)
                {
                    var ctor = constructors[c];
                    var attributes = ctor.GetAttributes();
                    bool hasAttribute = false;

                    for (int a = 0; a < attributes.Length; a++)
                    {
                        var attrClass = attributes[a].AttributeClass;
                        if (attrClass != null && attrClass.Name == "HubconConstructorAttribute")
                        {
                            var ns = attrClass.ContainingNamespace?.ToDisplayString();
                            if (ns == "Hubcon" || (ns != null && ns.StartsWith("Hubcon.")))
                            {
                                hasAttribute = true;
                                break;
                            }
                        }
                    }

                    if (hasAttribute)
                    {
                        chosenConstructor = ctor;
                        break;
                    }
                }

                if (chosenConstructor == null && constructors.Length > 0)
                {
                    chosenConstructor = constructors[0];
                }

                var parametersSb = new StringBuilder();
                if (chosenConstructor != null)
                {
                    var parameters = chosenConstructor.Parameters;
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        var paramType = parameters[p].Type.ToDisplayString();
                        parametersSb.Append($"x.GetRequiredService<{paramType}>()");

                        if (p < parameters.Length - 1)
                        {
                            parametersSb.Append(", ");
                        }
                    }
                }

                var comma = (i == count - 1) ? "" : ",";
                sb.AppendLine(
                    $"                 {{ typeof({serviceTypeString}), (Func<IServiceProvider, object>)(static x => new {serviceTypeString}({parametersSb})) }}{comma}");
            }

            sb.AppendLine("             };");
            sb.AppendLine();
            sb.AppendLine("             _factories = factories.ToImmutableDictionary();");
            sb.AppendLine("             Hubcon.FactoryMetadata.Setup(_factories);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var code = sb.ToString();
            spc.AddSource(fileName, SourceText.From(code, Encoding.UTF8));
        }
    }
}