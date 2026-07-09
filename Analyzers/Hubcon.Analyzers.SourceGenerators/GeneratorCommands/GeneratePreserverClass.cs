using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Hubcon.Analyzers.SourceGenerators.GeneratorCommands
{
    public static class GeneratePreserverClass
    {
        public static string Execute(INamedTypeSymbol typeSymbol, string classSuffix)
        {
            var sb = new StringBuilder();
            var preserverName = typeSymbol.Name + classSuffix;
            var ifaceFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";
            var fullProxyName = hasNamespace ? $"{namespaceName}.{preserverName}" : preserverName;

            sb.AppendLine($"{baseIndent}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {preserverName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");

            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({ifaceFullName}))]");

            sb.AppendLine($"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors, typeof({ifaceFullName}))]");
            

            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");
            sb.AppendLine($"{baseIndent}        {preserverName}Preserver();");

            sb.AppendLine($"{baseIndent}        {Tools.GetCondition()}");
            sb.AppendLine($"{baseIndent}        {{");

            var constructors = typeSymbol.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public ||
                            c.DeclaredAccessibility == Accessibility.Internal)
                .ToList();

            var allMembers = typeSymbol.GetMembers().Concat(typeSymbol.AllInterfaces.SelectMany(it => it.GetMembers())).ToList();
            if (constructors.Count == 0)
            {
                GeneratePreservationBlock(sb, baseIndent, fullProxyName, ifaceFullName, allMembers, null, 0);
            }
            else
            {
                for (int cIdx = 0; cIdx < constructors.Count; cIdx++)
                {
                    GeneratePreservationBlock(sb, baseIndent, fullProxyName, ifaceFullName, allMembers,
                        constructors[cIdx], cIdx);
                }
            }

            sb.AppendLine($"{baseIndent}        }}");
            sb.AppendLine($"{baseIndent}    }}");
            sb.AppendLine($"{baseIndent}");
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({fullProxyName}))]");
            sb.AppendLine($"{baseIndent}    public static void {preserverName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return sb.ToString();
        }

        private static void GeneratePreservationBlock(
            StringBuilder sb,
            string baseIndent,
            string fullProxyName,
            string ifaceFullName,
            List<ISymbol> allMembers,
            IMethodSymbol constructor,
            int constructorIndex)
        {
            string ctorArgs = "";
            if (constructor != null)
            {
                ctorArgs = string.Join(", ", constructor.Parameters.Select(p =>
                    $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})default!"));
            }

            var pVar = $"p_c{constructorIndex}";
            var iVar = $"i_c{constructorIndex}";

            sb.AppendLine($"{baseIndent}            var {pVar} = new {fullProxyName}({ctorArgs});");
            sb.AppendLine($"{baseIndent}            {ifaceFullName} {iVar} = ({ifaceFullName}){pVar};");

            int methodIndex = 0;
            foreach (var member in allMembers)
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary && (method.DeclaredAccessibility == Accessibility.Public || method.DeclaredAccessibility == Accessibility.Internal))
                {
                    methodIndex++;
                    var paramsList = string.Join(", ", method.Parameters.Select(p =>
                        $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})default!"));

                    if (method.ReturnsVoid)
                    {
                        sb.AppendLine($"{baseIndent}            {pVar}.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            {iVar}.{method.Name}({paramsList});");
                    }
                    else
                    {
                        var rA = $"r_c{constructorIndex}_m{methodIndex}a";
                        var rI = $"r_c{constructorIndex}_m{methodIndex}i";

                        sb.AppendLine($"{baseIndent}            var {rA} = {pVar}.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            var {rI} = {iVar}.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            _ = {rA}?.GetHashCode();");
                        sb.AppendLine($"{baseIndent}            _ = {rI}?.GetHashCode();");
                    }
                }
                else if (member is IPropertySymbol prop)
                {
                    sb.AppendLine($"{baseIndent}            _ = {pVar}.{prop.Name}?.GetHashCode();");
                    sb.AppendLine($"{baseIndent}            _ = {iVar}.{prop.Name}?.GetHashCode();");
                    
                    if (!prop.IsReadOnly)
                    {
                        sb.AppendLine($"{baseIndent}            {pVar}.{prop.Name} = default!;");
                        sb.AppendLine($"{baseIndent}            {iVar}.{prop.Name} = default!;");
                    }
                }
            }

            sb.AppendLine();
        }
    }
}