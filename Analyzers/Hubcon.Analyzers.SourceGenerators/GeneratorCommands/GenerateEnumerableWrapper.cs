using System.Collections.Generic;
using System.Text;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Hubcon.Analyzers.SourceGenerators.GeneratorCommands
{
    public class GenerateEnumerableWrapper
    {
        public static void Execute(SourceProductionContext spc, IEnumerable<INamedTypeSymbol> interfaces, string fileName)
        {
            var asyncTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var interfaceSymbol in interfaces)
            {
                foreach (var method in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    foreach (var param in method.Parameters)
                        param.Type.CollectAsyncTypesTo(asyncTypes);

                    method.ReturnType.CollectAsyncTypesTo(asyncTypes);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using Hubcon.Shared.Core.Tools;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class EnumerableRegistry");
            sb.AppendLine("    {");
            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine("        public static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine(
                "            Hubcon.Shared.Core.Tools.EnumerableTools.SetupEnumerableWrapper(GlobalWrapper);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine(
                "        private static IAsyncEnumerable<JsonElement> GlobalWrapper(object source, Type t, JsonTypeInfo info, CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine("            return t switch");
            sb.AppendLine("            {");

            foreach (var type in asyncTypes)
            {
                // Usamos el nombre completo con global:: para el cast y el typeof
                string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Pattern matching: matcheamos el tipo t contra el typeof concreto
                sb.AppendLine(
                    $@"                Type _ when t == typeof({fullName}) => EnumerableTools.GenericYieldWrapper(source as IAsyncEnumerable<{fullName}>, info, ct),");
            }

            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var code = sb.ToString();
            spc.AddSource($"AsyncEnumerableWrapper.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }
}