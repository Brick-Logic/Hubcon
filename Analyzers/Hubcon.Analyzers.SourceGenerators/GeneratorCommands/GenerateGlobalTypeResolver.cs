using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Hubcon.Analyzers.SourceGenerators.GeneratorCommands
{
    public class GenerateGlobalTypeResolver
    {
        public static void Execute(SourceProductionContext spc, List<string> allResolverNames, string fileName)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            sb.AppendLine($"namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public class HubconInitialization");
            sb.AppendLine("    {");
            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine("        public static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            var options = new JsonSerializerOptions");
            sb.AppendLine("            {");
            sb.AppendLine("                TypeInfoResolver = JsonTypeInfoResolver.Combine(");

            for (int i = 0; i < allResolverNames.Count; i++)
            {
                var separator = (i == allResolverNames.Count - 1) ? "" : ",";

                string memberAccess = allResolverNames[i] == "Hubcon.Shared.Core.Serialization.SystemTypesContext"
                    ? "Default"
                    : "Instance";

                sb.AppendLine($"                    {allResolverNames[i]}.{memberAccess}{separator}");
            }

            sb.AppendLine("                ),");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            Hubcon.Shared.Core.Serialization.HubconSerialization.SetupJsonSerializerOption(options);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var code = sb.ToString();
            spc.AddSource("HubconGlobalInitializer.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }
}