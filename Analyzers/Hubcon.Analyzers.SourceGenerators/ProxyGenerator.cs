using Hubcon.Analyzers.SourceGenerators.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using HubconAnalyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HubconAnalyzers.SourceGenerators
{
    [Generator]
    public class CommunicationProxyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Capturamos interfaces del proyecto actual
            var localInterfaces = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is InterfaceDeclarationSyntax,
                    transform: (ctx, _) =>
                    {
                        var iface = (InterfaceDeclarationSyntax)ctx.Node;
                        var symbol = ctx.SemanticModel.GetDeclaredSymbol(iface) as INamedTypeSymbol;
                        return GetValidContractInterface(symbol);
                    })
                .Where(symbol => symbol != null)
                .Collect();

            // Capturamos todas las referencias de compilación para buscar interfaces en proyectos referenciados
            var referencedInterfaces = context.CompilationProvider
                .Select((compilation, _) =>
                {
                    var interfaces = new List<INamedTypeSymbol>();

                    // Recorremos todos los assemblies referenciados
                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                        {
                            CollectInterfacesFromAssembly(assembly.GlobalNamespace, interfaces);
                        }
                    }

                    return interfaces.ToArray();
                });

            // Combinamos ambos sources
            var allInterfaces = localInterfaces
                .Combine(referencedInterfaces)
                .Select((combined, _) =>
                {
                    var (local, referenced) = combined;
                    return local.Concat(referenced).Distinct(SymbolEqualityComparer.Default).ToArray();
                });

            context.RegisterSourceOutput(allInterfaces, (spc, interfaceList) =>
            {
                // 1. HashSet para evitar procesar la misma interfaz dos veces (evita el error de hintName)
                var processedFullNames = new HashSet<string>();
                var generatedResolverClasses = new List<string>(); // Para el Aggregator

                // 1. Obtener la compilación (necesaria para buscar símbolos)
                var firstInterface = interfaceList.OfType<INamedTypeSymbol>().FirstOrDefault();
                if (firstInterface == null) return;

                var compilation = firstInterface.ContainingAssembly.GlobalNamespace.ContainingCompilation;

                var typesToSerialize = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

                foreach (var iface in interfaceList.OfType<INamedTypeSymbol>())
                {
                    var fullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Si ya procesamos este nombre completo (de la interfaz), saltamos
                    if (!processedFullNames.Add(fullName)) continue;

                    // 2. Crear un hintName único basado en el nombre completo de la interfaz
                    var safeHintName = fullName.Replace("global::", "").Replace(".", "_").Replace("<", "_").Replace(">", "_");

                    // 3. Recolección RECURSIVA de tipos (Esto es lo que llena el Resolver)

                    foreach (var member in iface.GetMembers())
                    {
                        if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                        {
                            // Extraer de Retorno (desempaqueta Task<T>)
                            CollectTypesRecursive(method.ReturnType, typesToSerialize);

                            // Extraer de Parámetros
                            foreach (var p in method.Parameters)
                            {
                                CollectTypesRecursive(p.Type, typesToSerialize);
                            }
                        }
                        else if (member is IPropertySymbol prop)
                        {
                            // Extraer de Propiedades (por si la interfaz tiene properties)
                            CollectTypesRecursive(prop.Type, typesToSerialize);
                        }
                    }

                    typesToSerialize.Add(iface);

                    // 5. Generar el Proxy Class
                    var proxyCode = GenerateProxyClass(iface);
                    spc.AddSource($"{safeHintName}Proxy.g.cs", SourceText.From(proxyCode, Encoding.UTF8));
                }

                var filteredTypes = typesToSerialize
                .Where(t =>
                {
                    // 1. Ignorar tipos con errores de compilación
                    if (t.TypeKind == TypeKind.Error) return false;

                    if (t.ToDisplayString() == "System.Text.Json.JsonElement") return true;

                    // 2. Ignorar tipos básicos (int, string, bool, etc.) 
                    // STJ ya sabe cómo manejarlos, no necesitan metadatos generados.
                    if (t.SpecialType != SpecialType.None) return false;

                    // 3. Ignorar punteros y tipos no seguros (causa del error sbyte*)
                    if (t.TypeKind == TypeKind.Pointer || t.TypeKind == TypeKind.FunctionPointer) return false;

                    var ns = t.ContainingNamespace?.ToDisplayString() ?? "";

                    // 4. Filtro de Namespaces: Solo permitimos tus tipos.
                    // Bloqueamos System por completo (ya que Task, String, etc., se filtran arriba)
                    // Bloqueamos Microsoft y tipos internos.
                    return !ns.StartsWith("System") &&
                           !ns.StartsWith("Microsoft") &&
                           !ns.StartsWith("<global namespace>") &&
                           !string.IsNullOrWhiteSpace(ns);
                })
                .ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

                var resolverClassName = $"GlobalMetadataResolver";
                generatedResolverClasses.Add($"Hubcon.Generated.{resolverClassName}");

                // 4. Generar el Resolver de Metadatos (Incluyendo el .Instance y el mapa de tipos)
                var resolverCode = GenerateMetadataResolver(resolverClassName, filteredTypes);
                spc.AddSource($"{resolverClassName}.g.cs", resolverCode);

                generatedResolverClasses.Add("Hubcon.Shared.Core.Serialization.SystemTypesContext");
                // Al final, generas el archivo global
                if (generatedResolverClasses.Any())
                {
                    var globalCode = GenerateGlobalResolver(generatedResolverClasses, "Hubcon.Generated");
                    spc.AddSource("HubconGlobalSerialization.g.cs", globalCode);
                }
            });
        }

        private static INamedTypeSymbol GetValidContractInterface(INamedTypeSymbol symbol)
        {
            if (symbol == null)
                return null;

            // Chequeamos que implemente IControllerContract
            var implementsContract = symbol.AllInterfaces
                .Any(i => i.Name == nameof(IControllerContract));

            return implementsContract ? symbol : null;
        }

        private static void CollectInterfacesFromAssembly(INamespaceSymbol namespaceSymbol, List<INamedTypeSymbol> interfaces)
        {
            // Recorremos todos los tipos en el namespace
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
                {
                    var validInterface = GetValidContractInterface(namedType);
                    if (validInterface != null)
                    {
                        interfaces.Add(validInterface);
                    }
                }
                else if (member is INamespaceSymbol childNamespace)
                {
                    // Recursivamente exploramos namespaces anidados
                    CollectInterfacesFromAssembly(childNamespace, interfaces);
                }
            }
        }

        private static string GenerateProxyClass(INamedTypeSymbol iface)
        {
            var proxyName = iface.Name + "Proxy";
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var sb = new StringBuilder();

            sb.AppendLine($"// Generated by Hubcon.Analyzers.SourceGenerators v1.0.0-rc55");
            sb.AppendLine($"");
            sb.AppendLine($"#nullable enable");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Models;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interceptor;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interfaces;");
            sb.AppendLine($"using Hubcon.Shared.Core.Attributes;");
            sb.AppendLine($"using Hubcon.Client.Core.Proxies;");
            sb.AppendLine($"using System.Collections.Generic;");
            sb.AppendLine($"using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine($"using System.Reflection;");
            sb.AppendLine($"using System.ComponentModel;");
            sb.AppendLine($"using System.Text.Json;");
            sb.AppendLine($"using System.Text.Json.Serialization;");
            sb.AppendLine($"using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine($"using System.Runtime.CompilerServices;");
            sb.AppendLine($"");

            // Determinamos el nivel de indentación base
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";

            // Solo agregamos el namespace si no es el global
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine($"{{");
            }

            sb.AppendLine($"{baseIndent}[HubconProxy]");
            sb.AppendLine($"{baseIndent}[EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public class {proxyName} : {"BaseContractProxy"}, {iface.ToDisplayString()}");
            sb.AppendLine($"{baseIndent}{{");
            sb.AppendLine($"");

            foreach (var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var accessors = "get;";

                if (property.SetMethod != null)
                    accessors += " set;";

                var type = $"{baseIndent}    public {property.Type.ToString()} {property.Name} {{ {accessors} }}";

                sb.AppendLine(type);
            }

            foreach (var method in iface
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
            {
                var returnType = method.ReturnType.ToDisplayString();
                var methodName = method.Name;
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var paramNames = string.Join(", ", method.Parameters.Select(p => p.Name));

                sb.AppendLine($"{baseIndent}    public {returnType} {methodName}({parameters})");
                sb.AppendLine($"{baseIndent}    {{");

                var stringMethodName = $"\"{method.GetMethodSymbolSignature()}\"";
                var callMethod = "";


                var nonTokenParams = method.Parameters
                    .Where(p => !p.Type.Name.Equals("CancellationToken", StringComparison.OrdinalIgnoreCase))
                    .ToList();


                string AllParameters = ", null";

                if (nonTokenParams.Any())
                {
                    AllParameters = $", new Dictionary<string, object?>() {{ ";
                    bool first = true;

                    foreach (var param in nonTokenParams)
                    {
                        if (param.Type.Name.ToLower().Contains("cancellationtoken".ToLower()))
                            continue;

                        if (first)
                        {
                            AllParameters += $"{{ \"{param.Name}\", {param.Name} }}";
                            first = false;
                        }
                        else
                        {
                            AllParameters += $", {{ \"{param.Name}\", {param.Name} }}";
                        }
                    }

                    AllParameters += $" }}";
                }

                string cancellationTokenName = ", default";

                if (method.Parameters.Any(x => x.Type.Name.ToLower().Contains("CancellationToken".ToLower())))
                {
                    cancellationTokenName = ", " + method.Parameters.First(x => x.Type.Name.ToLower().Contains("CancellationToken".ToLower())).Name;
                }

                if (returnType == "void")
                {
                    // CallAsync que devuelve Task, bloquea con Wait() para void
                    callMethod = $"{nameof(BaseProxy.CallAsync)}({stringMethodName}{AllParameters}{cancellationTokenName}).Wait();";
                }
                else if (returnType.StartsWith("System.Collections.Generic.IAsyncEnumerable<"))
                {
                    // Streaming
                    var generic = ExtractGenericArgument(returnType, "System.Collections.Generic.IAsyncEnumerable");
                    callMethod = $"return {nameof(BaseProxy.StreamAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else if (method.Parameters.Any(p => IsIAsyncEnumerable(p.Type)))
                {
                    // Si tiene argumento IAsyncEnumerable, usar IngestAsync
                    if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                    {
                        var generic = ExtractGenericArgument(returnType, "System.Threading.Tasks.Task");
                        callMethod = $"return {nameof(BaseProxy.IngestAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                    else if (returnType == "System.Threading.Tasks.Task")
                    {
                        callMethod = $"return {nameof(BaseProxy.IngestAsync)}({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                    else
                    {
                        // En source generator .NET Standard 2.0 no se usa excepción, puede fallar en runtime si llega acá.
                        callMethod = $"return {nameof(BaseProxy.IngestAsync)}<{returnType}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    // InvokeAsync para Task<T>
                    var generic = ExtractGenericArgument(returnType, "System.Threading.Tasks.Task");
                    callMethod = $"return {nameof(BaseProxy.InvokeAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else if (returnType == "System.Threading.Tasks.Task")
                {
                    // CallAsync para Task
                    callMethod = $"return {nameof(BaseProxy.CallAsync)}({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else
                {
                    // InvokeAsync para cualquier otro tipo sincrónico (bloquea con .Result)
                    callMethod = $"return {nameof(BaseProxy.InvokeAsync)}<{returnType}>({stringMethodName}{AllParameters}{cancellationTokenName}).Result;";
                }

                sb.AppendLine($"{baseIndent}        {callMethod}");
                sb.AppendLine($"{baseIndent}    }}");
            }

            sb.AppendLine($"{baseIndent}}}");
            sb.AppendLine("");

            var preserver = GenerateProxyPreserverClass(iface);
            sb.AppendLine(preserver);

            // Cerramos el namespace si lo abrimos
            if (hasNamespace)
            {
                sb.AppendLine($"}}");
            }

            return sb.ToString();
        }

        private static bool IsIAsyncEnumerable(ITypeSymbol type)
        {
            return type is INamedTypeSymbol namedType &&
                   namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IAsyncEnumerable<T>";
        }

        private static string ExtractGenericArgument(string fullTypeName, string genericTypeName)
        {
            // Ej: fullTypeName = "System.Threading.Tasks.Task<System.Int32>"
            //     genericTypeName = "System.Threading.Tasks.Task"
            // Resultado esperado: "System.Int32"

            int start = genericTypeName.Length + 1; // salto el '<'
            int end = fullTypeName.LastIndexOf('>');
            if (start >= end || start < 0 || end < 0)
                return "System.Object"; // fallback seguro

            return fullTypeName.Substring(start, end - start);
        }


        private static string ExtractTaskGenericArgumentRegex(string taskType)
        {
            // Patrón que captura todo entre el primer < y el último > balanceado
            var pattern = @"System\.Threading\.Tasks\.Task<(.+)>$";
            var match = Regex.Match(taskType, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "object";
        }

        private static string GenerateProxyPreserverClass(INamedTypeSymbol iface)
        {
            var sb = new StringBuilder();
            var proxyName = iface.Name + "Proxy";
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";

            // El PreserverModule también va en el mismo namespace con la indentación correcta
            sb.AppendLine($"{baseIndent}[EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {proxyName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");
            sb.AppendLine($"{baseIndent}    [ModuleInitializer]");
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");

            // Si está en un namespace, necesitamos el nombre completo
            var fullProxyName = hasNamespace
                ? $"{namespaceName}.{proxyName}"
                : proxyName;

            sb.AppendLine($"{baseIndent}        _ = typeof({fullProxyName});");
            sb.AppendLine($"{baseIndent}    }}");
            sb.AppendLine($"{baseIndent}");
            sb.AppendLine($"{baseIndent}    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({fullProxyName}))]");
            sb.AppendLine($"{baseIndent}    public static void {proxyName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return sb.ToString();
        }

        private static string GenerateMetadataResolver(string resolverName, ImmutableHashSet<ITypeSymbol> typesToSerialize)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace Hubcon.Generated {{");

            sb.AppendLine($"public class {resolverName} : IJsonTypeInfoResolver {{");
            sb.AppendLine($"    public static readonly {resolverName} Instance = new {resolverName}();");
            sb.AppendLine();

            sb.AppendLine("    public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options) {");

            // Switch de string basado en FullName
            sb.AppendLine("        return type.FullName switch {");

            foreach (var type in typesToSerialize)
            {
                // Usamos el FullName del símbolo (importante para el matching de runtime)
                var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                   .Replace("global::", ""); // El FullName de runtime no lleva 'global::'

                var safeName = GetSafeName(type);
                sb.AppendLine($"            \"{fullName}\" => Create_{safeName}(options),");
            }

            // Caso especial para JsonElement
            sb.AppendLine($"            \"System.Text.Json.JsonElement\" => Create_System_Text_Json_JsonElement(options),");

            sb.AppendLine("            _ => null");
            sb.AppendLine("        };");
            sb.AppendLine("    }");

            // ... (El resto de GenerateTypeMetadataMethod y Create_System_Text_Json_JsonElement sigue igual)
            foreach (var type in typesToSerialize)
            {
                GenerateTypeMetadataMethod(sb, type, optionsName: "options");
            }

            sb.AppendLine(@"
    private JsonTypeInfo Create_System_Text_Json_JsonElement(JsonSerializerOptions options)
    {
        return JsonMetadataServices.CreateValueInfo<global::System.Text.Json.JsonElement>(options, JsonMetadataServices.JsonElementConverter);
    }");

            sb.AppendLine("}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateTypeMetadataMethod(StringBuilder sb, ITypeSymbol type, string optionsName)
        {
            // 1. ESCUDO: Si es un tipo del sistema (como string) o un puntero, NO generamos método Create_...
            // Estos tipos ya los maneja STJ internamente.
            if (type.SpecialType != SpecialType.None ||
                type.ContainingNamespace?.ToDisplayString().StartsWith("System") == true)
            {
                return;
            }

            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var safeName = GetSafeName(type);

            sb.AppendLine($"    private JsonTypeInfo Create_{safeName}(JsonSerializerOptions {optionsName}) {{");

            // CASO ENUMS... (Igual que antes)
            if (type.TypeKind == TypeKind.Enum) { /* ... */ }

            // CASO COLECCIONES... (Igual que antes)
            else if (IsCollection(type, out var elementType)) { /* ... */ }

            // CASO OBJETOS
            else
            {
                sb.AppendLine($"        var info = JsonMetadataServices.CreateObjectInfo<{fullName}>({optionsName}, new JsonObjectInfoValues<{fullName}> {{");

                // --- LÓGICA DE CONSTRUCTOR PROTEGIDA ---
                var namedType = type as INamedTypeSymbol;
                var constructor = namedType?.Constructors
                    .OrderByDescending(c => c.Parameters.Length)
                    .FirstOrDefault(c => c.DeclaredAccessibility == Accessibility.Public);

                // Si el constructor tiene punteros, lo ignoramos por completo
                bool hasPointers = constructor?.Parameters.Any(p => p.Type.TypeKind == TypeKind.Pointer) ?? false;

                if (constructor != null && constructor.Parameters.Length > 0 && !hasPointers)
                {
                    sb.AppendLine("            ConstructorParameterMetadataInitializer = () => new JsonParameterInfoValues[] {");
                    foreach (var p in constructor.Parameters)
                    {
                        var pType = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        sb.AppendLine($@"                new JsonParameterInfoValues {{ 
                    Name = ""{p.Name}"", 
                    ParameterType = typeof({pType}), 
                    Position = {p.Ordinal} 
                }},");
                    }
                    sb.AppendLine("            },");

                    var args = string.Join(", ", constructor.Parameters.Select((p, i) => $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})args[{i}]"));
                    sb.AppendLine($"            ObjectWithParameterizedConstructorCreator = (args) => new {fullName}({args}),");
                }
                else if (namedType != null && !namedType.IsAbstract)
                {
                    sb.AppendLine($"            ObjectCreator = () => new {fullName}(),");
                }

                // --- LÓGICA DE PROPIEDADES ---
                sb.AppendLine("            PropertyMetadataInitializer = (context) => new JsonPropertyInfo[] {");
                foreach (var prop in type.GetMembers().OfType<IPropertySymbol>().Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic))
                {
                    var pType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Aquí está el truco: STJ usará su propio resolver para tipos básicos (string, int) 
                    // aunque nosotros no les generemos un Create_... manual.
                    sb.AppendLine($"                JsonMetadataServices.CreatePropertyInfo<{pType}>({optionsName}, new JsonPropertyInfoValues<{pType}> {{");
                    sb.AppendLine($"                    PropertyName = \"{prop.Name}\",");
                    sb.AppendLine($"                    Getter = (obj) => (({fullName})obj).{prop.Name},");
                    sb.AppendLine($"                    PropertyTypeInfo = options.GetTypeInfo(typeof({pType})),");
                    sb.AppendLine($"                    DeclaringType = typeof({fullName}),");
                    sb.AppendLine($"                    JsonPropertyName = \"{prop.Name}\",");
                    sb.AppendLine($"                    IsProperty = true,");
                    sb.AppendLine($"                    IsPublic = true,");

                    bool canSet = !prop.IsReadOnly && (prop.SetMethod == null || !prop.SetMethod.IsInitOnly);
                    if (canSet) sb.AppendLine($"                    Setter = (obj, val) => (({fullName})obj).{prop.Name} = val,");

                    sb.AppendLine("                }),");
                }
                sb.AppendLine("            }");
                sb.AppendLine("        });");
                sb.AppendLine("        return info;");
            }
            sb.AppendLine("    }");
        }

        private static bool IsCollection(ITypeSymbol type, out ITypeSymbol elementType)
        {
            elementType = null;

            // Caso Array T[]
            if (type is IArrayTypeSymbol arrayType)
            {
                elementType = arrayType.ElementType;
                return true;
            }

            // Caso IEnumerable<T> o derivados (List<T>, etc.)
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T ||
                    namedType.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
                {
                    elementType = namedType.TypeArguments.FirstOrDefault();
                    return elementType != null;
                }
            }

            return false;
        }

        private static string GetSafeName(ITypeSymbol type)
        {
            return type.ToDisplayString()
                .Replace("global::", "")
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("[", "Array")
                .Replace("]", "")
                .Replace(",", "_")
                .Replace("?", "Nullable") // Manejo de T?
                .Replace(" ", "");
        }

        private static void CollectTypesRecursive(ITypeSymbol type, HashSet<ITypeSymbol> typesToSerialize)
        {
            if (type == null) return;

            // 1. Filtro de seguridad: Ignorar namespaces de sistema pesados
            var ns = type.ContainingNamespace?.ToDisplayString();
            if (ns != null && (ns.StartsWith("System.Reflection") ||
                               ns.StartsWith("System.Runtime") ||
                               ns.StartsWith("Microsoft.CodeAnalysis")))
            {
                return;
            }

            // 2. Desempaquetar Task<T> o Nullable<T>
            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                // Si es Task<T>, procesamos el T y salimos (no queremos serializar el Task en sí)
                if (named.Name == "Task" && ns == "System.Threading.Tasks")
                {
                    CollectTypesRecursive(named.TypeArguments[0], typesToSerialize);
                    return;
                }
            }

            // 3. Evitar duplicados y recursión infinita
            if (!typesToSerialize.Add(type)) return;

            // 4. Si es colección, recolectar el tipo del elemento
            if (IsCollection(type, out var elementType))
            {
                CollectTypesRecursive(elementType, typesToSerialize);
            }
            // 5. Si es objeto, recolectar tipos de sus propiedades
            else if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
            {
                foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
                {
                    CollectTypesRecursive(prop.Type, typesToSerialize);
                }
            }
        }

        private static string GenerateGlobalResolver(List<string> allResolverNames, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;"); // Para JsonIgnoreCondition
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName} {{");
            sb.AppendLine("    public static class HubconSerialization");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions");
            sb.AppendLine("        {");
            sb.AppendLine("            TypeInfoResolver = JsonTypeInfoResolver.Combine(");

            for (int i = 0; i < allResolverNames.Count; i++)
            {
                var separator = (i == allResolverNames.Count - 1) ? "" : ",";

                if (allResolverNames[i] == "Hubcon.Shared.Core.Serialization.SystemTypesContext")
                {
                    sb.AppendLine($"                {allResolverNames[i]}.Default{separator}");
                }
                else
                {
                    sb.AppendLine($"                {allResolverNames[i]}.Instance{separator}");
                }
            }

            sb.AppendLine("            ),");
            sb.AppendLine("            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
            sb.AppendLine("            WriteIndented = false,");
            sb.AppendLine("            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,");
            sb.AppendLine("            MaxDepth = 64,");
            sb.AppendLine("            PropertyNameCaseInsensitive = true,");
            sb.AppendLine("            Converters = { new JsonStringEnumConverter() }");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}