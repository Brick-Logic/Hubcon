using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;

namespace Hubcon.Analyzers.SourceGenerators.GeneratorCommands
{
    public static class GeneratePreserverForClass
    {
        public static (string code, string preserverMethod) Execute(INamedTypeSymbol typeSymbol, string preserverName, string indentOffset)
        {
            var sb = new StringBuilder();
            var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var preserverMethod = $"{preserverName}PreserverModule.Init();";

            var baseIndent = indentOffset;

            sb.AppendLine(
                $"{baseIndent}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {preserverName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");

            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({typeFullName}))]");
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors, typeof({typeFullName}))]");
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(Microsoft.Extensions.Primitives.StringValues))]");
            
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");
            sb.AppendLine($"{baseIndent}        {preserverName}Preserver();");

            sb.AppendLine($"{baseIndent}        {Tools.GetCondition()}");
            sb.AppendLine($"{baseIndent}        {{");

            // Filtramos todos los constructores públicos de instancia
            var constructors = typeSymbol.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .ToList();

            // Preservamos TODAS las interfaces que la clase implementa
            var allInterfaces = typeSymbol.AllInterfaces.ToList();

            // Extraemos todos los miembros de la clase y de sus interfaces
            var allMembers = typeSymbol.GetMembers()
                .Concat(allInterfaces.SelectMany(it => it.GetMembers()))
                .ToList();

            if (constructors.Count == 0)
            {
                GeneratePreservationBlock(sb, typeSymbol, baseIndent, typeFullName, allMembers, allInterfaces, null, 0);
            }
            else
            {
                for (int cIdx = 0; cIdx < constructors.Count; cIdx++)
                {
                    GeneratePreservationBlock(sb, typeSymbol, baseIndent, typeFullName, allMembers, allInterfaces,
                        constructors[cIdx], cIdx);
                }
            }

            sb.AppendLine($"{baseIndent}        }}");
            sb.AppendLine($"{baseIndent}    }}");
            sb.AppendLine($"{baseIndent}");
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({preserverName}PreserverModule))]");
            sb.AppendLine($"{baseIndent}    public static void {preserverName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return (sb.ToString(), preserverMethod);
        }

        private static void GeneratePreservationBlock(StringBuilder sb,
            INamedTypeSymbol typeSymbol,
            string baseIndent,
            string typeFullName,
            List<ISymbol> allMembers,
            List<INamedTypeSymbol> interfaces,
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

            sb.AppendLine($"{baseIndent}            var {pVar} = new {typeFullName}({ctorArgs});");

            ProcessMembersPreservation(sb, typeSymbol, baseIndent, allMembers, pVar, constructorIndex, "gen");

            int ifaceIndex = 0;
            
            foreach (var iface in interfaces)
            {
                ifaceIndex++;
                var ifaceTypeFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var ifaceVar = $"c_i{constructorIndex}_f{ifaceIndex}";

                sb.AppendLine($"{baseIndent}            {ifaceTypeFullName} {ifaceVar} = ({ifaceTypeFullName}){pVar};");

                var ifaceMembers = iface.GetMembers().ToList();
                ProcessMembersPreservation(sb, typeSymbol,  baseIndent, ifaceMembers, ifaceVar, constructorIndex,
                    $"iface{ifaceIndex}");
            }

            sb.AppendLine();
        }

        private static void ProcessMembersPreservation(StringBuilder sb,
            INamedTypeSymbol typeSymbol,
            string baseIndent,
            List<ISymbol> members,
            string targetVar,
            int constructorIndex,
            string suffix)
        {
            int methodIndex = 0;
            
            var attributes = new List<AttributeData>();

            foreach (var attr in typeSymbol.GetAttributes())
            {
                attributes.Add(attr);
            }
            
            foreach (var member in members)
            {
                var attributeArray = member.GetAllParameterAndPropertyAttributes();
                attributes.AddRange(attributeArray);
                
                if (member is IMethodSymbol method && !method.IsGenericMethod && method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && method.DeclaredAccessibility == Accessibility.Internal)
                {
                    methodIndex++;
                    var paramsList = string.Join(", ", method.Parameters.Select(p =>
                        $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})default!"));

                    if (method.ReturnsVoid)
                    {
                        sb.AppendLine($"{baseIndent}            {targetVar}.{method.Name}({paramsList});");
                    }
                    else
                    {
                        var rI = $"r_c{constructorIndex}_m{methodIndex}_{suffix}";

                        sb.AppendLine($"{baseIndent}            var {rI} = {targetVar}.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            _ = {rI}.GetHashCode();");
                    }
                }
                else if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public && prop.DeclaredAccessibility == Accessibility.Internal)
                {
                    sb.AppendLine($"{baseIndent}            _ = {targetVar}.{prop.Name}.GetHashCode();");

                    if (!prop.IsReadOnly)
                    {
                        sb.AppendLine($"{baseIndent}            {targetVar}.{prop.Name} = default!;");
                    }
                }
            }
            
            var distinctAttributeTypes = attributes
                .Select(a => a.AttributeClass)
                .Where(c => c != null)
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<ITypeSymbol>()
                .ToList();

            foreach (var attribute in distinctAttributeTypes)
            {
                if (attribute != null)
                {
                    var name = $"attr_{targetVar}_{attribute.GetSafeName()}";
                    
                    sb.AppendLine($"{baseIndent}            var {name} = typeof({typeSymbol.ToDisplayString()}).GetCustomAttribute<{attribute.ToDisplayString()}>();");
                    sb.AppendLine($"{baseIndent}            {name}?.GetHashCode();");
                }
            }
        }
    }
}