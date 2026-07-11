using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Hubcon.Analyzers.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hubcon.Analyzers.SourceGenerators
{
    public static class SymbolTools
    {
        public static bool IsCandidateClass(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classSyntax
                   && classSyntax.BaseList != null
                   && !classSyntax.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword);
        }

        public static INamedTypeSymbol GetClassSymbolIfImplementsInterface(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

            if (classSymbol == null)
            {
                return null;
            }

            var implementsIndirectly = classSymbol.AllInterfaces
                .Any(namedInterface => namedInterface.ImplementsControllerContract());

            return implementsIndirectly ? classSymbol : null;
        }

        public static void CollectInterfacesFromAssemblyTo(this IAssemblySymbol assemblySymbol,
            List<INamedTypeSymbol> interfaces, INamespaceSymbol nameSpace = null)
        {
            var namespaceSymbol = nameSpace ?? assemblySymbol.GlobalNamespace;
            // Recorremos todos los tipos en el namespace
            var members = namespaceSymbol.GetMembers();

            var name = assemblySymbol.Name;

            foreach (var member in members)
            {
                if (member is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
                {
                    if (namedType.ImplementsControllerContract())
                    {
                        interfaces.Add(namedType);
                    }
                }
                else if (member is INamespaceSymbol childNamespace)
                {
                    // Recursivamente exploramos namespaces anidados
                    assemblySymbol.CollectInterfacesFromAssemblyTo(interfaces, childNamespace);
                }
            }
        }

        public static void MapMethodAttributes(Endpoint endpoint, StringBuilder sb)
        {
            foreach (var attr in endpoint.CombinedAttributes)
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null) continue;

                var attrNamespace = attrClass.ContainingNamespace?.ToDisplayString();

                if (attrNamespace == "System.Runtime.CompilerServices")
                {
                    continue;
                }

                string attrFullName = attrClass.ToDisplayString();

                if (attrFullName == "System.Reflection.DefaultMemberAttribute")
                {
                    continue;
                }

                string formattedAttr = FormatAttribute(attr);
                sb.AppendLine($"        {formattedAttr}");
            }
        }

        public static void MapPropertyAttributes(Endpoint endpoint, StringBuilder sb, bool useBodyAttributes)
        {
            foreach (var param in endpoint.Parameters)
            {
                if (param.Type.ToDisplayString() == "System.Threading.CancellationToken")
                    continue;
                
                string typeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string paramName = param.Name;

                bool hasExplicitBindingAttribute = false;

                foreach (var attr in param.Attributes)
                {
                    var attrClass = attr.AttributeClass;
                    if (attrClass == null) continue;

                    var attrNamespace = attrClass.ContainingNamespace?.ToDisplayString();

                    if (attrNamespace == "System.Runtime.CompilerServices")
                    {
                        continue;
                    }

                    string attrFullName = attrClass.ToDisplayString();

                    if (attrFullName == "System.Reflection.DefaultMemberAttribute")
                    {
                        continue;
                    }

                    if (attrFullName.Contains("Microsoft.AspNetCore.Http.Metadata") ||
                        attrFullName.Contains("Microsoft.AspNetCore.Mvc.From") ||
                        attrFullName.Contains("AsParametersAttribute"))
                    {
                        hasExplicitBindingAttribute = true;
                    }

                    // Formateamos y escribimos el atributo 1:1 con sus argumentos originales
                    string formattedAttr = FormatAttribute(attr);
                    sb.AppendLine($"        {formattedAttr}");
                }

                if (useBodyAttributes && !hasExplicitBindingAttribute)
                {
                    bool isSimpleType = param.Type.IsValueType ||
                                        param.Type.SpecialType == SpecialType.System_String ||
                                        param.Type.ToDisplayString() == "System.DateTime" ||
                                        param.Type.ToDisplayString() == "System.TimeSpan" ||
                                        param.Type.ToDisplayString() == "System.Guid";

                    if (isSimpleType)
                    {
                        sb.AppendLine($"        [Microsoft.AspNetCore.Mvc.FromQuery(Name = \"{paramName}\")]");
                    }
                    else
                    {
                        sb.AppendLine("        [Microsoft.AspNetCore.Mvc.FromBody]");
                    }
                }

                var isNullable = false;

                if (useBodyAttributes && param.HasExplicitDefaultValue)
                {
                    object defaultVal = param.ExplicitDefaultValue;
                    string valStr;

                    if (defaultVal == null)
                    {
                        isNullable = true;
                    }
                    else
                    {
                        switch (defaultVal)
                        {
                            case string s:
                                valStr = "\"" + s + "\"";
                                break;
                            case bool b:
                                valStr = b ? "true" : "false";
                                break;
                            case double d:
                                valStr = d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d";
                                break;
                            case float f:
                                valStr = f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f";
                                break;
                            case decimal dec:
                                valStr = dec.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
                                break;
                            default:
                                valStr = defaultVal.ToString();
                                break;
                        }

                        sb.AppendLine($"        [System.ComponentModel.DefaultValue({valStr})]");
                    }
                }

                sb.AppendLine(
                    $"        public {typeName}{(isNullable ? "?" : "")} {paramName} {{ get; set; }}");
                sb.AppendLine();
            }
        }

        private static string FormatAttribute(AttributeData attr)
        {
            var attrName = attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var args = new List<string>();

            // 1. Argumentos Posicionales (Constructor)
            foreach (var posArg in attr.ConstructorArguments)
            {
                args.Add(FormatTypedConstant(posArg));
            }

            // 2. Argumentos Nombrados (Propiedades/Fields seteados explícitamente)
            foreach (var namedArg in attr.NamedArguments)
            {
                args.Add($"{namedArg.Key} = {FormatTypedConstant(namedArg.Value)}");
            }

            if (args.Count > 0)
            {
                return $"[{attrName}({string.Join(", ", args)})]";
            }

            return $"[{attrName}]";
        }

        private static string FormatTypedConstant(TypedConstant constant)
        {
            if (constant.Kind == TypedConstantKind.Array)
            {
                var elements = constant.Values.Select(FormatTypedConstant);
                return $"new[] {{ {string.Join(", ", elements)} }}";
            }

            if (constant.Value == null)
            {
                return "null";
            }

            if (constant.Kind == TypedConstantKind.Enum)
            {
                return $"({constant.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){constant.Value}";
            }

            if (constant.Value is bool b)
            {
                return b ? "true" : "false";
            }

            if (constant.Value is string s)
            {
                return $"\"{s.Replace("\"", "\\\"")}\""; // Escapamos comillas internas por las dudas
            }

            if (constant.Value is char c)
            {
                return $"'{c}'";
            }

            if (constant.Value is double d)
            {
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d";
            }

            if (constant.Value is float f)
            {
                return f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f";
            }

            if (constant.Value is decimal dec)
            {
                return dec.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
            }

            if (constant.Value is ITypeSymbol typeSymbol)
            {
                return $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
            }

            return constant.Value.ToString();
        }
        
        public static INamedTypeSymbol GetSymbolIfHasPreserveAttribute(GeneratorSyntaxContext ctx)
        {
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
            if (symbol == null) return null;

            var attributes = symbol.GetAttributes();
            for (int i = 0; i < attributes.Length; i++)
            {
                var attrClass = attributes[i].AttributeClass;
                if (attrClass != null &&
                    attrClass.Name == "HubconPreserveAttribute" &&
                    attrClass.ContainingNamespace?.ToDisplayString() == "Hubcon")
                {
                    return symbol;
                }
            }

            return null;
        }

        public static bool HasPreserveAttribute(INamedTypeSymbol symbol)
        {
            if (symbol == null) return false;

            var attributes = symbol.GetAttributes();
            for (int i = 0; i < attributes.Length; i++)
            {
                var attrClass = attributes[i].AttributeClass;
                if (attrClass != null &&
                    attrClass.Name == "HubconPreserveAttribute" &&
                    attrClass.ContainingNamespace?.ToDisplayString() == "Hubcon")
                {
                    return true;
                }
            }

            return false;
        }

        public static HashSet<INamedTypeSymbol> ExpandPreservedSymbols(Compilation compilation,
            System.Collections.Immutable.ImmutableArray<INamedTypeSymbol> markedSymbols)
        {
            var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            var targetInterfaces = markedSymbols.Where(s => s.TypeKind == TypeKind.Interface)
                .ToImmutableHashSet(SymbolEqualityComparer.Default);
            var targetClasses = markedSymbols.Where(s => s.TypeKind == TypeKind.Class)
                .ToImmutableHashSet(SymbolEqualityComparer.Default);

            foreach (var targetClass in targetClasses)
            {
                if (targetClass is INamedTypeSymbol symbol)
                    result.Add(symbol);
            }

            // Acción unificada para evaluar si una clase debe ser preservada
            Action<INamedTypeSymbol> evaluateClass = currentClass =>
            {
                if (targetInterfaces.Count > 0)
                {
                    var interfaces = currentClass.AllInterfaces;
                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        if (targetInterfaces.Contains(interfaces[i]))
                        {
                            result.Add(currentClass);
                            return;
                        }
                    }
                }

                if (targetClasses.Count > 0)
                {
                    var baseType = currentClass.BaseType;
                    while (baseType != null)
                    {
                        if (targetClasses.Contains(baseType))
                        {
                            result.Add(currentClass);
                            return;
                        }

                        baseType = baseType.BaseType;
                    }
                }
            };

            GetAllAssemblyClasses(compilation.GlobalNamespace, evaluateClass);

            foreach (var reference in compilation.References)
            {
                var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (assemblySymbol != null)
                {
                    var name = assemblySymbol.Name;
                    if (name == "Hubcon" || name.StartsWith("Hubcon."))
                    {
                        GetAllAssemblyClasses(assemblySymbol.GlobalNamespace, evaluateClass);
                    }
                }
            }

            return result;
        }

        public static void GetAllAssemblyClasses(INamespaceSymbol namespaceSymbol,
            Action<INamedTypeSymbol> onClassFound)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol nestedNamespace)
                {
                    GetAllAssemblyClasses(nestedNamespace, onClassFound);
                }
                else if (member is INamedTypeSymbol typeSymbol)
                {
                    if (typeSymbol.TypeKind == TypeKind.Class)
                    {
                        onClassFound(typeSymbol);
                    }

                    if (typeSymbol.GetTypeMembers().Length > 0)
                    {
                        ProcessNestedTypes(typeSymbol, onClassFound);
                    }
                }
            }
        }

        public static void CollectMarkedTypesInNamespace(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol nestedNs)
                {
                    CollectMarkedTypesInNamespace(nestedNs, results);
                }
                else if (member is INamedTypeSymbol type)
                {
                    if (member.ContainingNamespace.Name != "Hubcon")
                        return;

                    if (HasPreserveAttribute(type))
                    {
                        results.Add(type);
                    }

                    if (type.GetTypeMembers().Length > 0)
                    {
                        CollectMarkedTypesInNested(type, results);
                    }
                }
            }
        }

        public static void CollectMarkedTypesInNested(INamedTypeSymbol type, List<INamedTypeSymbol> results)
        {
            var nestedTypes = type.GetTypeMembers();
            for (int i = 0; i < nestedTypes.Length; i++)
            {
                var nestedType = nestedTypes[i];

                if (HasPreserveAttribute(nestedType))
                {
                    results.Add(nestedType);
                }

                if (nestedType.GetTypeMembers().Length > 0)
                {
                    CollectMarkedTypesInNested(nestedType, results);
                }
            }
        }

        public static void ProcessNestedTypes(INamedTypeSymbol typeSymbol, Action<INamedTypeSymbol> onClassFound)
        {
            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                if (nestedType.TypeKind == TypeKind.Class)
                {
                    onClassFound(nestedType);
                }

                if (nestedType.GetTypeMembers().Length > 0)
                {
                    ProcessNestedTypes(nestedType, onClassFound);
                }
            }
        }
    }
}