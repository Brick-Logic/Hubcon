using Hubcon;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
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
using System.Text.RegularExpressions;

namespace HubconAnalyzers.SourceGenerators
{
    [Generator]
    public class CommunicationProxyGenerator : IIncrementalGenerator
    {
        private static INamedTypeSymbol hubconResponseBaseSymbol;

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

                    if (hubconResponseBaseSymbol == null)
                        hubconResponseBaseSymbol = compilation.GetTypeByMetadataName("Hubcon.HubconResponse`1");

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

                var typesToSerialize = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var interfaces = interfaceList.OfType<INamedTypeSymbol>();

                foreach (var iface in interfaces)
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
                    }

                    // 5. Generar el Proxy Class
                    var proxyCode = GenerateProxyClass(iface);
                    spc.AddSource($"{safeHintName}Proxy.g.cs", SourceText.From(proxyCode, Encoding.UTF8));
                }

                var proxyLookupCode = GenerateProxyRegistry(interfaces);
                spc.AddSource($"ProxyLookup.g.cs", SourceText.From(proxyLookupCode, Encoding.UTF8));

                var enumerableWrapperCode = GenerateEnumerableWrapper(interfaces);
                spc.AddSource($"AsyncEnumerableWrapper.g.cs", SourceText.From(enumerableWrapperCode, Encoding.UTF8));

                var semiFilteredTypes = typesToSerialize
                .Where(t =>
                {
                    // 1. Ignorar tipos con errores de compilación
                    if (t.TypeKind == TypeKind.Error) return false;

                    // 2. CASO ESPECIAL: Arrays (int[], string[], etc.)
                    if (t is IArrayTypeSymbol) return true;

                    // 3. CASO ESPECIAL: Nullable (int?, etc.)
                    if (t.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return true;

                    // 4. Exclusión explícita de JsonElement (se maneja manual)
                    if (t.ToDisplayString() == "global::System.Text.Json.JsonElement") return false;

                    // 5. Ignorar tipos básicos de sistema (int, string, bool, etc.)
                    // Pero dejamos pasar si son tipos complejos (SpecialType.None)
                    if (t.SpecialType != SpecialType.None) return false;

                    // 6. Ignorar punteros (causa del error sbyte*)
                    if (t.TypeKind == TypeKind.Pointer || t.TypeKind == TypeKind.FunctionPointer) return false;

                    var ns = t.ContainingNamespace?.ToDisplayString() ?? "";

                    // 7. CASO ESPECIAL: Colecciones Genéricas (List<T>, IEnumerable<T>, Dictionary<K,V>)
                    if (ns.StartsWith("System.Collections.Generic"))
                    {
                        // Solo dejamos pasar si el tipo es una instancia genérica cerrada (ej: List<int>)
                        if (t is INamedTypeSymbol named && named.IsGenericType) return true;
                    }

                    // 8. Filtro de Namespaces: Solo permitimos tus tipos.
                    // Bloqueamos System y Microsoft, a menos que hayan pasado por los filtros de arriba.
                    return !ns.StartsWith("System") &&
                           !ns.StartsWith("Microsoft") &&
                           !ns.StartsWith("<global namespace>") &&
                           !string.IsNullOrWhiteSpace(ns);
                }).ToList();

                var subscriptionHandlersCode = GenerateSubscriptionHandlerFactory(interfaces, semiFilteredTypes);
                spc.AddSource("ClientSubscriptionFactory.g.cs", subscriptionHandlersCode);

                var filteredTypes = semiFilteredTypes.ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

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

            sb.AppendLine($"// Generated by Hubcon.Analyzers.SourceGenerators");
            sb.AppendLine($"");
            sb.AppendLine($"#nullable enable");
            sb.AppendLine($"using Hubcon;");
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
            sb.AppendLine($"{baseIndent}    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof({proxyName}))]");
            sb.AppendLine($"{baseIndent}    public {proxyName}() {{ }}");
            sb.AppendLine();

            foreach (var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var propertyName = property.Name;
                var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Generamos un nombre para el backing field (ej: _myProperty)
                var fieldName = "_" + char.ToLower(propertyName[0]) + propertyName.Substring(1);

                // 1. Escribimos el backing field privado
                sb.AppendLine($"{baseIndent}    private {propertyType} {fieldName};");

                // 2. Empezamos la propiedad pública
                sb.AppendLine($"{baseIndent}    public {propertyType} {propertyName}");
                sb.AppendLine($"{baseIndent}    {{");

                // Get con su backing field
                sb.AppendLine($"{baseIndent}        get => this.{fieldName};");

                // Set con su backing field (si existe)
                if (property.SetMethod != null)
                {
                    sb.AppendLine($"{baseIndent}        set => this.{fieldName} = value;");
                }

                sb.AppendLine($"{baseIndent}    }}");
                sb.AppendLine(); // Espacio entre propiedades
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

            sb.AppendLine("");
            sb.AppendLine($"{baseIndent}    public override void SetPropertyValue(string propertyName, object value)");
            sb.AppendLine($"{baseIndent}    {{");
            sb.AppendLine($"{baseIndent}        switch (propertyName)");
            sb.AppendLine($"{baseIndent}        {{");

            foreach (var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var propertyName = property.Name;
                var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Reutilizamos la misma convención de nombre para el backing field
                var fieldName = "_" + char.ToLower(propertyName[0]) + propertyName.Substring(1);

                // Generamos el case para cada propiedad
                sb.AppendLine($"{baseIndent}            case \"{propertyName}\":");
                sb.AppendLine($"{baseIndent}                this.{fieldName} = value as {propertyType};");
                sb.AppendLine($"{baseIndent}                break;");
            }

            sb.AppendLine($"{baseIndent}        }}");
            sb.AppendLine($"{baseIndent}    }}");
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
            var ifaceFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";
            var fullProxyName = hasNamespace ? $"{namespaceName}.{proxyName}" : proxyName;

            sb.AppendLine($"{baseIndent}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {proxyName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");

            // --- CAMBIO CLAVE 1: Preservar la interfaz misma ---
            sb.AppendLine($"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({ifaceFullName}))]");

            // --- CAMBIO CLAVE 2: DynamicDependency por cada método para forzar la VTable ---
            var allMembers = iface.GetMembers().Concat(iface.AllInterfaces.SelectMany(it => it.GetMembers())).ToList();
            foreach (var member in allMembers)
            {
                sb.AppendLine($"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(\"{member.Name}\", typeof({fullProxyName}))]");
            }

            sb.AppendLine($"{baseIndent}    [System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");
            sb.AppendLine($"{baseIndent}        {proxyName}Preserver();");

            sb.AppendLine($"{baseIndent}        Console.WriteLine(\"Modulo preserver ({fullProxyName}) cargado.\");");
            sb.AppendLine($"{baseIndent}        if (System.Guid.NewGuid().ToString() == \"preserver\")");
            sb.AppendLine($"{baseIndent}        {{");

            // Constructor vacío
            sb.AppendLine($"{baseIndent}            var p = new {fullProxyName}();");
            sb.AppendLine($"{baseIndent}            {ifaceFullName} i = ({ifaceFullName})p;");

            int methodIndex = 0;
            foreach (var member in allMembers)
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    methodIndex++;
                    var paramsList = string.Join(", ", method.Parameters.Select(p =>
                        $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})default!"));

                    if (method.ReturnsVoid)
                    {
                        sb.AppendLine($"{baseIndent}            p.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            i.{method.Name}({paramsList});");
                    }
                    else
                    {
                        // Usamos variables únicas r{index}a y r{index}i
                        sb.AppendLine($"{baseIndent}            var r{methodIndex}a = p.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            var r{methodIndex}i = i.{method.Name}({paramsList});");
                        sb.AppendLine($"{baseIndent}            _ = r{methodIndex}a?.GetHashCode();");
                        sb.AppendLine($"{baseIndent}            _ = r{methodIndex}i?.GetHashCode();");
                    }
                }
                else if (member is IPropertySymbol prop)
                {
                    sb.AppendLine($"{baseIndent}            _ = p.{prop.Name}?.GetHashCode();");
                    sb.AppendLine($"{baseIndent}            _ = i.{prop.Name}?.GetHashCode();");
                    if (!prop.IsReadOnly)
                    {
                        sb.AppendLine($"{baseIndent}            p.{prop.Name} = default!;");
                        sb.AppendLine($"{baseIndent}            i.{prop.Name} = default!;");
                    }
                }
            }
            sb.AppendLine($"{baseIndent}            p.SetPropertyValue(default!, default!);");

            sb.AppendLine($"{baseIndent}        }}");
            sb.AppendLine($"{baseIndent}    }}");
            sb.AppendLine($"{baseIndent}");
            sb.AppendLine($"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({fullProxyName}))]");
            sb.AppendLine($"{baseIndent}    public static void {proxyName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return sb.ToString();
        }

        private static string GenerateMetadataResolver(string resolverName, ImmutableHashSet<ITypeSymbol> typesToSerialize)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
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

            // Cambiamos el switch para que use comparaciones de Type directamente
            sb.AppendLine("        return type switch {");

            foreach (var type in typesToSerialize)
            {
                // Usamos el formato calificado completo (global::...) para el typeof
                var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var safeName = GetSafeName(type);

                // C# 8.0/Standard 2.1 syntax: Type t when t == typeof(...)
                sb.AppendLine($"            Type t when t == typeof({fullName}) => Create_{safeName}(options),");
            }

            // Caso especial para JsonElement
            sb.AppendLine($"            Type t when t == typeof(global::System.Text.Json.JsonElement) => Create_System_Text_Json_JsonElement(options),");

            sb.AppendLine("            _ => null");
            sb.AppendLine("        };");
            sb.AppendLine("    }");

            List<string> methodNames = new List<string>();

            // Generar los métodos Create_{SafeName}
            foreach (var type in typesToSerialize)
            {
                var name = GetSafeName(type);

                if (methodNames.Any(x => x.ToLower() == name.ToLower()))
                    continue;

                methodNames.Add(name);
                GenerateTypeMetadataMethod(sb, type, optionsName: "options");
            }

            sb.AppendLine(@"
    private JsonTypeInfo Create_System_Text_Json_JsonElement(JsonSerializerOptions options)
    {
        return JsonMetadataServices.CreateValueInfo<global::System.Text.Json.JsonElement>(options, JsonMetadataServices.JsonElementConverter);
    }");

            sb.AppendLine("}"); // class
            sb.AppendLine("}"); // namespace

            return sb.ToString();
        }

        private static void GenerateTypeMetadataMethod(StringBuilder sb, ITypeSymbol type, string optionsName)
        {
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var safeName = GetSafeName(type);

            sb.AppendLine($"    private JsonTypeInfo Create_{safeName}(JsonSerializerOptions {optionsName}) {{");

            // Lógica específica para Nullables
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var innerType = (type as INamedTypeSymbol)?.TypeArguments.First();
                var innerFullName = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine($@"
        var converter = JsonMetadataServices.GetNullableConverter<{innerFullName}>({optionsName});
        return JsonMetadataServices.CreateValueInfo<{fullName}>({optionsName}, converter);");
            }
            else
            {
                // 2. CASO ENUMS
                if (type.TypeKind == TypeKind.Enum)
                {
                    // Usamos el convertidor genérico que es 100% compatible con AOT
                    sb.AppendLine($@"
        var enumConverter = global::Hubcon.HubconEnumConverter<{fullName}>.Current;
        return JsonMetadataServices.CreateValueInfo<{fullName}>({optionsName}, enumConverter);");
                }
                else if (IsDictionary(type, out var keyType, out var valueType))
                {
                    var keyFullName = keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var valueFullName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    string creator = (type is INamedTypeSymbol n && !n.IsAbstract) ? $"() => new {fullName}()" : "null";

                    sb.AppendLine($@"
        var info = JsonMetadataServices.CreateIDictionaryInfo<{fullName}, {keyFullName}, {valueFullName}>(
            {optionsName},
            new JsonCollectionInfoValues<{fullName}> {{
                ObjectCreator = {creator},
                KeyInfo = {optionsName}.GetTypeInfo(typeof({keyFullName})),
                NumberHandling = default,
                SerializeHandler = null
            }}
        );
        return info;");
                }
                // 3. CASO COLECCIONES (List<T>, T[], etc.)
                else if (IsCollection(type, out var elementType))
                {
                    var elementFullName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Determinamos la implementación concreta para el ObjectCreator
                    string creator = "null";
                    if (type is IArrayTypeSymbol)
                    {
                        // Para arrays, el creador suele ser nulo o manejado internamente por STJ
                        creator = "null";
                    }
                    else if (type.TypeKind == TypeKind.Class && !type.IsAbstract)
                    {
                        creator = $"() => new {fullName}()";
                    }

                    sb.AppendLine($@"
        var info = JsonMetadataServices.CreateIEnumerableInfo<{fullName}, {elementFullName}>(
            {optionsName}, 
            new JsonCollectionInfoValues<{fullName}> {{
                ObjectCreator = {creator},
                KeyInfo = null,
                ElementInfo = {optionsName}.GetTypeInfo(typeof({elementFullName})),
                NumberHandling = default,
                SerializeHandler = null // STJ lo resuelve internamente si ElementInfo es válido
            }}
        );
        return info;");
                }
                else
                {
                    sb.AppendLine($"        var info = JsonMetadataServices.CreateObjectInfo<{fullName}>({optionsName}, new JsonObjectInfoValues<{fullName}> {{");

                    // --- LÓGICA DE CONSTRUCTOR PROTEGIDA ---
                    var namedType = type as INamedTypeSymbol;

                    var constructor = namedType?.Constructors
                        .OrderByDescending(c => c.Parameters.Length)
                        .FirstOrDefault(c => 
                        c.DeclaredAccessibility == Accessibility.Public 
                        && c.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonConstructorAttribute"));

                    if(constructor == null )
                    {
                        constructor = namedType?.Constructors
                        .OrderByDescending(c => c.Parameters.Length)
                        .FirstOrDefault(c => c.DeclaredAccessibility == Accessibility.Public);
                    }

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
                        // 1. Soporte para [JsonIgnore]
                        var isIgnored = prop.GetAttributes().Any(a =>
                            a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute");

                        if (isIgnored) continue;

                        // 2. Soporte para [JsonPropertyName("...")]
                        var jsonNameAttr = prop.GetAttributes().FirstOrDefault(a =>
                            a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute");

                        // Si tiene el atributo usamos el valor definido, si no, el nombre de la propiedad en C#
                        string jsonPropertyName = jsonNameAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? prop.Name;

                        var pType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        sb.AppendLine($"                JsonMetadataServices.CreatePropertyInfo<{pType}>({optionsName}, new JsonPropertyInfoValues<{pType}> {{");
                        sb.AppendLine($"                    PropertyName = \"{prop.Name}\","); // Nombre real en el código C#
                        sb.AppendLine($"                    JsonPropertyName = \"{jsonPropertyName}\","); // Nombre que aparecerá en el JSON
                        sb.AppendLine($"                    Getter = (obj) => (({fullName})obj).{prop.Name},");
                        sb.AppendLine($"                    PropertyTypeInfo = {optionsName}.GetTypeInfo(typeof({pType})),");
                        sb.AppendLine($"                    DeclaringType = typeof({fullName}),");
                        sb.AppendLine($"                    IsProperty = true,");
                        sb.AppendLine($"                    IsPublic = true,");

                        bool canSet = !prop.IsReadOnly && (prop.SetMethod == null || !prop.SetMethod.IsInitOnly);
                        if (canSet)
                            sb.AppendLine($"                    Setter = (obj, val) => (({fullName})obj).{prop.Name} = val,");

                        sb.AppendLine("                }),");
                    }
                    sb.AppendLine("            }");
                    sb.AppendLine("        });");
                    sb.AppendLine("        return info;");
                }
            }
            sb.AppendLine("    }");
        }

        private static bool IsDictionary(ITypeSymbol type, out ITypeSymbol keyType, out ITypeSymbol valueType)
        {
            keyType = null;
            valueType = null;

            if (type is INamedTypeSymbol namedType)
            {
                // Buscamos en el tipo mismo y en todas sus interfaces
                var interfaceType = namedType.AllInterfaces
                    .Concat(new[] { namedType })
                    .FirstOrDefault(i => i.IsGenericType &&
                        (i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T || // Caso base
                         i.OriginalDefinition.ToDisplayString().StartsWith("System.Collections.Generic.IDictionary<") ||
                         i.OriginalDefinition.ToDisplayString().StartsWith("System.Collections.Generic.IReadOnlyDictionary<") ||
                         i.OriginalDefinition.ToDisplayString().StartsWith("System.Collections.Generic.Dictionary<")));

                if (interfaceType != null && interfaceType.TypeArguments.Length == 2)
                {
                    keyType = interfaceType.TypeArguments[0];
                    valueType = interfaceType.TypeArguments[1];
                    return true;
                }
            }
            return false;
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
            if (type == null || type.TypeKind == TypeKind.Error) return;

            // 1. Filtros de namespaces (Reflection, etc.)
            var ns = type.ContainingNamespace?.ToDisplayString();
            if (ns != null && (ns.StartsWith("System.Reflection") || ns.StartsWith("Microsoft.CodeAnalysis"))) return;

            // 2. Manejo de Arrays
            if (type is IArrayTypeSymbol arrayType)
            {
                if (typesToSerialize.Add(type))
                {
                    CollectTypesRecursive(arrayType.ElementType, typesToSerialize);
                }
                return;
            }

            if (type is INamedTypeSymbol named)
            {
                // 3. Caso especial Task<T>: Desempaquetar y salir (No queremos Task en el JSON)
                if (named.IsGenericType && named.Name == "Task" && ns == "System.Threading.Tasks")
                {
                    foreach (var arg in named.TypeArguments) CollectTypesRecursive(arg, typesToSerialize);
                    return;
                }

                // 4. Agregar el tipo actual (sea List<User>, User, o int?)
                // Si ya estaba, cortamos para evitar bucles infinitos
                if (!typesToSerialize.Add(type)) return;

                // --- NUEVO: Generar HubconResponse<T> ---
                // Solo si el tipo actual NO es ya un HubconResponse y no es un tipo primitivo de sistema basura
                if (type.SpecialType != SpecialType.System_Void
                    && type.SpecialType != SpecialType.System_Object
                    && hubconResponseBaseSymbol != null
                    && !SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, hubconResponseBaseSymbol))
                {
                    // Fabricamos HubconResponse<TipoActual>
                    var wrappedType = hubconResponseBaseSymbol.Construct(type);
                    typesToSerialize.Add(wrappedType);
                }

                // 5. Si es genérico (List<T>, Nullable<T>, Dictionary<K,V>)
                if (named.IsGenericType)
                {
                    // Entramos recursivamente en los tipos de adentro
                    foreach (var arg in named.TypeArguments)
                    {
                        CollectTypesRecursive(arg, typesToSerialize);
                    }

                    // Si es una colección de System, no queremos analizar sus propiedades (paso 6)
                    // porque STJ ya sabe cómo tratar una List.
                    if (ns != null && ns.StartsWith("System.Collections")) return;
                }

                // 6. Si es un modelo propio (Clase/Struct que no es de sistema)
                // Analizamos sus propiedades para seguir la cadena
                if ((type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
                    type.SpecialType == SpecialType.None)
                {
                    foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
                    {
                        if (prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic)
                        {
                            CollectTypesRecursive(prop.Type, typesToSerialize);
                        }
                    }
                }
            }
        }

        private static string GenerateGlobalResolver(List<string> allResolverNames, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            sb.AppendLine($"namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public class HubconSerialization");
            sb.AppendLine("    {");
            sb.AppendLine("        [ModuleInitializer]");
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
            sb.AppendLine("            // Registro automático en el framework");
            sb.AppendLine("            Hubcon.Shared.Core.Serialization.HubconSerialization.SetupJsonSerializerOption(options);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateSubscriptionHandlerFactory(IEnumerable<INamedTypeSymbol> interfaces, IList<ITypeSymbol> typesToSerialize)
        {
            // 1. Buscamos las interfaces que implementan IControllerContract
            var controllerContracts = interfaces.Where(i =>
                i.TypeKind == TypeKind.Interface &&
                (i.ToDisplayString() == "Hubcon.IControllerContract" ||
                 i.AllInterfaces.Any(ai => ai.ToDisplayString() == "Hubcon.IControllerContract")));

            // 2. Extraemos los argumentos genéricos T de las propiedades ISubscription<T>
            var genericTypes = controllerContracts
                .SelectMany(contract => contract.GetMembers().OfType<IPropertySymbol>())
                .Select(prop => prop.Type as INamedTypeSymbol)
                .Where(type => type != null && type.IsGenericType)
                .Where(type => type.OriginalDefinition.ToDisplayString() == "Hubcon.ISubscription<T>")
                .Select(type => type.TypeArguments.First());

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated;");
            sb.AppendLine();
            sb.AppendLine("public static class ClientSubscriptionFactory");
            sb.AppendLine("{");

            // Module Initializer para registro automático
            sb.AppendLine("    [ModuleInitializer]");
            sb.AppendLine("    public static void Initialize()");
            sb.AppendLine("    {");
            sb.AppendLine("        Hubcon.Client.Builder.SubscriptionFactory.SetupSubscriptionFactory(Create);");
            sb.AppendLine();
            sb.AppendLine("        // Preservación para AOT");
            sb.AppendLine("        if (Guid.NewGuid().ToString() == \"preserver\")");
            sb.AppendLine("        {");
            sb.AppendLine("             _ = Create(default!, default!);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ClientSubscriptionFactory))]");
            sb.AppendLine("    public static object Create(Type type, object config)");
            sb.AppendLine("    {");
            sb.AppendLine("        return type switch");
            sb.AppendLine("        {");

            // 3. Deduplicación y Generación de Ramas
            var uniqueTypes = genericTypes
                .Where(t => t != null)
                .GroupBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Select(g => g.First())
                .ToList();

            foreach (var type in uniqueTypes)
            {
                typesToSerialize.Add(type);
                string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Branch del switch
                sb.AppendLine($"            Type t when t == typeof({fullName}) => new Hubcon.Client.Core.Subscriptions.ClientSubscriptionHandler<{fullName}>(config as Hubcon.Client.Core.Subscriptions.ClientSubscriptionConfig<object>),");
            }

            sb.AppendLine("            _ => null");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public string GenerateProxyRegistry(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// 1.0.33");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;"); // Necesario para ModuleInitializer
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine("");
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class ProxyLookup");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Inicializa automáticamente el lookup de proxies en el framework.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [ModuleInitializer]");
            sb.AppendLine("        public static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Pasamos el método como Func<Type, Type?> al framework");
            sb.AppendLine("            Hubcon.Client.Builder.HubconClientBuilder.SetupProxyLookup(GetProxyType);");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ProxyLookup))]");
            sb.AppendLine("        public static Type? GetProxyType(Type interfaceType)");
            sb.AppendLine("        {");
            // Usamos FullName o AssemblyQualifiedName para evitar colisiones entre proyectos
            sb.AppendLine("            return interfaceType.FullName switch");
            sb.AppendLine("            {");

            foreach (var interfaceSymbol in interfaces)
            {
                // Usamos ToDisplayString sin global:: para el string del switch, 
                // pero typeof() sí necesita el path completo.
                string interfaceFullName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                string proxyFullPath = $"{interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}Proxy";

                sb.AppendLine($"                \"{interfaceFullName}\" => typeof({proxyFullPath}),");
            }

            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateEnumerableWrapper(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var asyncTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var interfaceSymbol in interfaces)
            {
                foreach (var method in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    foreach (var param in method.Parameters)
                        CheckAndAddAsyncType(param.Type, asyncTypes);

                    CheckAndAddAsyncType(method.ReturnType, asyncTypes);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
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
            sb.AppendLine("        [ModuleInitializer]");
            sb.AppendLine("        public static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            Hubcon.Shared.Core.Tools.EnumerableTools.SetupEnumerableWrapper(GlobalWrapper);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static IAsyncEnumerable<JsonElement> GlobalWrapper(object source, Type t, JsonTypeInfo info, CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine("            return t switch");
            sb.AppendLine("            {");

            foreach (var type in asyncTypes)
            {
                // Usamos el nombre completo con global:: para el cast y el typeof
                string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Pattern matching: matcheamos el tipo t contra el typeof concreto
                sb.AppendLine($@"                Type _ when t == typeof({fullName}) => EnumerableTools.GenericYieldWrapper(source as IAsyncEnumerable<{fullName}>, info, ct),");
            }

            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void CheckAndAddAsyncType(ITypeSymbol type, HashSet<ITypeSymbol> set)
        {
            if (type is INamedTypeSymbol named)
            {
                // Caso IAsyncEnumerable<T>
                if (named.Name == "IAsyncEnumerable" && named.IsGenericType)
                {
                    set.Add(named.TypeArguments[0]);
                }
                // Caso Task<IAsyncEnumerable<T>>
                else if (named.Name == "Task" && named.IsGenericType && named.TypeArguments[0] is INamedTypeSymbol inner)
                {
                    if (inner.Name == "IAsyncEnumerable" && inner.IsGenericType)
                    {
                        set.Add(inner.TypeArguments[0]);
                    }
                }
            }
        }
    }
}