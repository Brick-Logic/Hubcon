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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Hubcon.Analyzers.SourceGenerators;
using Hubcon.Analyzers.SourceGenerators.Extensions;

namespace HubconAnalyzers.SourceGenerators
{
    [Generator]
    public class CommunicationProxyGenerator : IIncrementalGenerator
    {
        private static INamedTypeSymbol _hubconResponseBaseSymbol;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var shouldExecuteForClient = context.GetHubconClientProvider();
            var shouldExecuteForServer = context.GetHubconServerProvider();
            var shouldExecute = shouldExecuteForClient.Combine(shouldExecuteForServer);

            var localInterfaces = context.CreateNext((ctx, _) =>
                {
                    var interfaceDeclarationSyntax = (InterfaceDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(interfaceDeclarationSyntax);
                    return symbol.ImplementsControllerContract() ? symbol : null;
                })
                .Where(symbol => symbol != null)
                .Collect();

            // Capturamos todas las referencias de compilación para buscar interfaces en proyectos referenciados
            var referencedInterfaces = context.CompilationProvider.Select((compilation, _) =>
            {
                var interfaces = new List<INamedTypeSymbol>();

                if (_hubconResponseBaseSymbol == null)
                    _hubconResponseBaseSymbol = SymbolExtensions.GetHubconResponseSymbol(compilation);

                // Recorremos todos los assemblies referenciados
                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                    {
                        assembly.CollectInterfacesFromAssemblyTo(interfaces);
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

            var finalProvider = allInterfaces
                .Combine(context.CompilationProvider.Select((c, _) => c.AssemblyName))
                .Combine(shouldExecute);

            context.RegisterSourceOutput(finalProvider, (spc, data) =>
            {
                var ((interfaceList, assemblyName), shouldGenerate) = data;

                var generateForClient = shouldGenerate.Left;
                var generateForServer = shouldGenerate.Right;

                if (generateForClient == false && generateForServer == false)
                    return;

                if (assemblyName == "Hubcon" || assemblyName == "Hubcon.Client" || assemblyName == "Hubcon.Server" ||
                    assemblyName.StartsWith("Hubcon."))
                    return;

                var processedFullNames = new HashSet<string>();
                var generatedResolverClasses = new List<string>();

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
                    var safeHintName = fullName.Replace("global::", "").Replace(".", "_").Replace("<", "_")
                        .Replace(">", "_");

                    // 3. Recolección RECURSIVA de tipos (esto es lo que llena el Resolver)

                    foreach (var member in iface.GetMembers())
                    {
                        if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                        {
                            // Extraer de Retorno (desempaqueta Task<T>)
                            method.ReturnType.CollectTypesRecursiveTo(typesToSerialize, _hubconResponseBaseSymbol);

                            // Extraer de Parámetros
                            foreach (var p in method.Parameters)
                            {
                                p.Type.CollectTypesRecursiveTo(typesToSerialize, _hubconResponseBaseSymbol);
                            }
                        }
                    }

                    // 5. Generar el Proxy Class
                    if (generateForClient)
                    {
                        var proxyCode = GenerateProxyClass(iface);
                        spc.AddSource($"{safeHintName}Proxy.g.cs", SourceText.From(proxyCode, Encoding.UTF8));
                    }
                }

                if (generateForClient)
                {
                    var proxyLookupCode = GenerateProxyRegistry(interfaces);
                    spc.AddSource($"ProxyLookup.g.cs", SourceText.From(proxyLookupCode, Encoding.UTF8));

                    var enumerableWrapperCode = GenerateEnumerableWrapper(interfaces);
                    spc.AddSource($"AsyncEnumerableWrapper.g.cs",
                        SourceText.From(enumerableWrapperCode, Encoding.UTF8));
                }

                if (!generateForClient || !generateForServer)
                {
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

                    var filteredTypes =
                        semiFilteredTypes.ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

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
                }

                if (generateForServer)
                {
                    var endpointModelsCode = GenerateEndpointParameterWrappers(interfaces);
                    spc.AddSource($"EndpointParameterWrappers.g.cs", SourceText.From(endpointModelsCode, Encoding.UTF8));

                    var endpointInvokersCode = GenerateDedicatedInvokers(interfaces);
                    spc.AddSource($"EndpointInvokers.g.cs", SourceText.From(endpointInvokersCode, Encoding.UTF8));

                    var endpointDelegates = GenerateHttpDelegates(interfaces);
                    spc.AddSource($"EndpointDelegates.g.cs", SourceText.From(endpointDelegates, Encoding.UTF8));
                }
            });
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
            sb.AppendLine($"{baseIndent}public class {proxyName} : BaseContractProxy, {iface.ToDisplayString()}");
            sb.AppendLine($"{baseIndent}{{");
            sb.AppendLine(
                $"{baseIndent}    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof({proxyName}))]");
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
                var parameters = string.Join(", ",
                    method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
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
                    cancellationTokenName = ", " + method.Parameters
                        .First(x => x.Type.Name.ToLower().Contains("CancellationToken".ToLower())).Name;
                }

                if (returnType == "void")
                {
                    // CallAsync que devuelve Task, bloquea con Wait() para void
                    callMethod =
                        $"{nameof(BaseProxy.CallAsync)}({stringMethodName}{AllParameters}{cancellationTokenName}).Wait();";
                }
                else if (returnType.StartsWith("System.Collections.Generic.IAsyncEnumerable<"))
                {
                    // Streaming
                    var generic = returnType.GetGenericArgument("System.Collections.Generic.IAsyncEnumerable");
                    callMethod =
                        $"return {nameof(BaseProxy.StreamAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else if (method.Parameters.Any(p => p.Type.IsIAsyncEnumerable()))
                {
                    // Si tiene argumento IAsyncEnumerable, usar IngestAsync
                    if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                    {
                        var generic = returnType.GetGenericArgument("System.Threading.Tasks.Task");
                        callMethod =
                            $"return {nameof(BaseProxy.IngestAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                    else if (returnType == "System.Threading.Tasks.Task")
                    {
                        callMethod =
                            $"return {nameof(BaseProxy.IngestAsync)}({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                    else
                    {
                        // En source generator .NET Standard 2.0 no se usa excepción, puede fallar en runtime si llega acá.
                        callMethod =
                            $"return {nameof(BaseProxy.IngestAsync)}<{returnType}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                    }
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    // InvokeAsync para Task<T>
                    var generic = returnType.GetGenericArgument("System.Threading.Tasks.Task");
                    callMethod =
                        $"return {nameof(BaseProxy.InvokeAsync)}<{generic}>({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else if (returnType == "System.Threading.Tasks.Task")
                {
                    // CallAsync para Task
                    callMethod =
                        $"return {nameof(BaseProxy.CallAsync)}({stringMethodName}{AllParameters}{cancellationTokenName});";
                }
                else
                {
                    // InvokeAsync para cualquier otro tipo sincrónico (bloquea con .Result)
                    callMethod =
                        $"return {nameof(BaseProxy.InvokeAsync)}<{returnType}>({stringMethodName}{AllParameters}{cancellationTokenName}).Result;";
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

        private static string GenerateProxyPreserverClass(INamedTypeSymbol iface)
        {
            var sb = new StringBuilder();
            var proxyName = iface.Name + "Proxy";
            var ifaceFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";
            var fullProxyName = hasNamespace ? $"{namespaceName}.{proxyName}" : proxyName;

            sb.AppendLine(
                $"{baseIndent}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {proxyName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");

            // --- CAMBIO CLAVE 1: Preservar la interfaz misma ---
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({ifaceFullName}))]");

            // --- CAMBIO CLAVE 2: DynamicDependency por cada método para forzar la VTable ---
            var allMembers = iface.GetMembers().Concat(iface.AllInterfaces.SelectMany(it => it.GetMembers())).ToList();
            foreach (var member in allMembers)
            {
                sb.AppendLine(
                    $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(\"{member.Name}\", typeof({fullProxyName}))]");
            }

            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");
            sb.AppendLine($"{baseIndent}        {proxyName}Preserver();");

            sb.AppendLine($"{baseIndent}        {Tools.GetCondition()}");
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
            sb.AppendLine(
                $"{baseIndent}    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({fullProxyName}))]");
            sb.AppendLine($"{baseIndent}    public static void {proxyName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return sb.ToString();
        }

        private static string GenerateMetadataResolver(string resolverName,
            ImmutableHashSet<ITypeSymbol> typesToSerialize)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Hubcon;");
            sb.AppendLine("using Hubcon.Shared.Abstractions.Attributes;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
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
                var safeName = type.GetSafeName();

                // C# 8.0/Standard 2.1 syntax: Type t when t == typeof(...)
                sb.AppendLine($"            Type t when t == typeof({fullName}) => Create_{safeName}(options),");
            }

            // Caso especial para JsonElement
            sb.AppendLine(
                $"            Type t when t == typeof(global::System.Text.Json.JsonElement) => Create_System_Text_Json_JsonElement(options),");

            sb.AppendLine("            _ => null");
            sb.AppendLine("        };");
            sb.AppendLine("    }");

            List<string> methodNames = new List<string>();

            // Generar los métodos Create_{SafeName}
            foreach (var type in typesToSerialize)
            {
                var name = type.GetSafeName();

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
            var safeName = type.GetSafeName();

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
                else if (type.IsDictionary(out var keyType, out var valueType))
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
                else if (type.IsCollection(out var elementType))
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
                    sb.AppendLine(
                        $"        var info = JsonMetadataServices.CreateObjectInfo<{fullName}>({optionsName}, new JsonObjectInfoValues<{fullName}> {{");

                    var namedType = type as INamedTypeSymbol;

                    var constructor = namedType?.Constructors
                        .OrderByDescending(c => c.Parameters.Length)
                        .FirstOrDefault(c =>
                            c.DeclaredAccessibility == Accessibility.Public
                            && c.GetAttributes().Any(a =>
                                a.AttributeClass?.ToDisplayString() ==
                                "System.Text.Json.Serialization.JsonConstructorAttribute"));

                    if (constructor == null)
                    {
                        constructor = namedType?.Constructors
                            .OrderByDescending(c => c.Parameters.Length)
                            .FirstOrDefault(c => c.DeclaredAccessibility == Accessibility.Public);
                    }

                    // Si el constructor tiene punteros, lo ignoramos por completo
                    bool hasPointers = constructor?.Parameters.Any(p => p.Type.TypeKind == TypeKind.Pointer) ?? false;

                    if (constructor != null && constructor.Parameters.Length > 0 && !hasPointers)
                    {
                        sb.AppendLine(
                            "            ConstructorParameterMetadataInitializer = () => new JsonParameterInfoValues[] {");
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

                        var args = string.Join(", ",
                            constructor.Parameters.Select((p, i) =>
                                $"({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})args[{i}]"));
                        sb.AppendLine(
                            $"            ObjectWithParameterizedConstructorCreator = (args) => new {fullName}({args}),");
                    }
                    else if (namedType != null && !namedType.IsAbstract)
                    {
                        sb.AppendLine($"            ObjectCreator = () => new {fullName}(),");
                    }

                    // --- LÓGICA DE PROPIEDADES ---
                    sb.AppendLine("            PropertyMetadataInitializer = (context) => new JsonPropertyInfo[] {");
                    foreach (var prop in type.GetMembers().OfType<IPropertySymbol>()
                                 .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic))
                    {
                        // 1. Soporte para [JsonIgnore]
                        var isIgnored = prop.GetAttributes().Any(a =>
                            a.AttributeClass?.ToDisplayString() ==
                            "System.Text.Json.Serialization.JsonIgnoreAttribute");

                        if (isIgnored) continue;

                        // 2. Soporte para [JsonPropertyName("...")]
                        var jsonNameAttr = prop.GetAttributes().FirstOrDefault(a =>
                            a.AttributeClass?.ToDisplayString() ==
                            "System.Text.Json.Serialization.JsonPropertyNameAttribute");

                        // Si tiene el atributo usamos el valor definido, si no, el nombre de la propiedad en C#
                        string jsonPropertyName =
                            jsonNameAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? prop.Name;

                        var pType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        sb.AppendLine(
                            $"                JsonMetadataServices.CreatePropertyInfo<{pType}>({optionsName}, new JsonPropertyInfoValues<{pType}> {{");
                        sb.AppendLine(
                            $"                    PropertyName = \"{prop.Name}\","); // Nombre real en el código C#
                        sb.AppendLine(
                            $"                    JsonPropertyName = \"{jsonPropertyName}\","); // Nombre que aparecerá en el JSON
                        sb.AppendLine($"                    Getter = (obj) => (({fullName})obj).{prop.Name},");
                        sb.AppendLine(
                            $"                    PropertyTypeInfo = {optionsName}.GetTypeInfo(typeof({pType})),");
                        sb.AppendLine($"                    DeclaringType = typeof({fullName}),");
                        sb.AppendLine($"                    IsProperty = true,");
                        sb.AppendLine($"                    IsPublic = true,");

                        bool canSet = !prop.IsReadOnly && (prop.SetMethod == null || !prop.SetMethod.IsInitOnly);
                        if (canSet)
                            sb.AppendLine(
                                $"                    Setter = (obj, val) => (({fullName})obj).{prop.Name} = val,");

                        sb.AppendLine("                }),");
                    }

                    sb.AppendLine("            }");
                    sb.AppendLine("        });");
                    sb.AppendLine("        return info;");
                }
            }

            sb.AppendLine("    }");
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
            sb.AppendLine("            // Registro automático en el framework");
            sb.AppendLine(
                "            Hubcon.Shared.Core.Serialization.HubconSerialization.SetupJsonSerializerOption(options);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateProxyRegistry(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
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
            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
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
                string interfaceFullName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", "");
                string proxyFullPath =
                    $"{interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}Proxy";

                sb.AppendLine($"                \"{interfaceFullName}\" => typeof({proxyFullPath}),");
            }

            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateEnumerableWrapper(IEnumerable<INamedTypeSymbol> interfaces)
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

            return sb.ToString();
        }

        private static string GenerateEndpointParameterWrappers(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Runtime.Serialization;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
            sb.AppendLine("using Microsoft.AspNetCore.Mvc.ModelBinding;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");

            sb.AppendLine($"    public static class ParameterWrapperProvider");
            sb.AppendLine("    {");
            sb.AppendLine(
                "        #if UNITY_2017_1_OR_NEWER\r\n        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]\r\n        #else\r\n        [ModuleInitializer]\r\n        #endif");
            sb.AppendLine("        public static void Init()");
            sb.AppendLine("        {");
            sb.AppendLine(
                "             global::Hubcon.EndpointManager.Setup(EndpointDelegateProvider.GetDelegate, EndpointInvokerProvider.GetInvoker, ParameterWrapperProvider.GetWrapperType, ParameterWrapperProvider.GetWrapperDelegate);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public static Type? GetWrapperType(string contractName, string signature)");
            sb.AppendLine("        {");
            sb.AppendLine("            var finalSignature = contractName + \"_\" + signature;");
            sb.AppendLine("            switch(finalSignature)");
            sb.AppendLine("            {");
            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);


                foreach (var method in methods)
                {
                    if (method.Parameters.Count(x => x.ToDisplayString() != "System.Threading.CancellationToken") == 0)
                        continue;

                    var wrapperClassName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Request";

                    sb.AppendLine(
                        $"                 case \"{interfaceSymbol.Name}_{method.GetMethodSymbolSignature()}\": return typeof({wrapperClassName});");
                }
            }

            sb.AppendLine("                 default: return null;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine();

            sb.AppendLine(
                "        public static Func<IReadOnlyDictionary<string, object>, object>? GetWrapperDelegate(string contractName, string signature)");
            sb.AppendLine("        {");
            sb.AppendLine("            var finalSignature = contractName + \"_\" + signature;");
            sb.AppendLine("            switch(finalSignature)");
            sb.AppendLine("            {");

            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);


                foreach (var method in methods)
                {
                    if (method.Parameters.Count(x => x.ToDisplayString() != "System.Threading.CancellationToken") == 0)
                        continue;

                    var wrapperClassName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Request";

                    sb.AppendLine(
                        $"                 case \"{interfaceSymbol.Name}_{method.GetMethodSymbolSignature()}\":return {wrapperClassName}.GetWrapped;");
                }
            }

            sb.AppendLine("                 default: return null;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);

                foreach (var method in methods)
                {
                    if (method.Parameters.Count(x => x.ToDisplayString() != "System.Threading.CancellationToken") == 0)
                        continue;

                    var wrapperClassName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Request";

                    sb.AppendLine($"    public sealed class {wrapperClassName} : global::Hubcon.IWrapper");
                    sb.AppendLine("    {");

                    var parameters = method.Parameters
                        .Where(x => x.ToDisplayString() != "System.Threading.CancellationToken")
                        .ToImmutableArray();

                    foreach (var param in parameters)
                    {
                        string typeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        string paramName = param.Name;

                        foreach (var attr in param.GetAttributes())
                        {
                            var attrNamespace = attr.AttributeClass?.ContainingNamespace?.ToDisplayString();

                            if (attrNamespace == "System.Runtime.CompilerServices")
                            {
                                continue;
                            }

                            sb.AppendLine($"        [{attr.AttributeClass.ToDisplayString()}]");
                        }

                        var isNullable = false;
                        // Clonamos el valor por defecto si existe
                        if (param.HasExplicitDefaultValue)
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

                                sb.AppendLine($"        [DefaultValue({valStr})]");
                            }
                        }

                        sb.AppendLine(
                            $"        public {typeName}{(isNullable ? "?" : "")} {paramName} {{ get; set; }}");
                        sb.AppendLine();
                    }

                    if (parameters.Length > 0)
                    {
                        sb.AppendLine(
                            $"        public static object GetWrapped(IReadOnlyDictionary<string, object> parameters_{method.GetMethodSymbolSignature()})");
                        sb.AppendLine("        {");
                        sb.AppendLine($"             var wrapped = new {wrapperClassName}();");

                        foreach (var parameter in parameters)
                        {
                            string typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            sb.AppendLine(
                                $"             wrapped.{parameter.Name} = ({typeName})parameters_{method.GetMethodSymbolSignature()}[\"{parameter.Name}\"];");
                        }

                        sb.AppendLine($"             return wrapped;");

                        sb.AppendLine("        }");
                        sb.AppendLine("");
                        sb.AppendLine(
                            $"        public void Populate(IReadOnlyDictionary<string, object> parameters_{method.GetMethodSymbolSignature()})");
                        sb.AppendLine("        {");
                        foreach (var parameter in parameters)
                        {
                            string typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            sb.AppendLine(
                                $"             {parameter.Name} = ({typeName})parameters_{method.GetMethodSymbolSignature()}[\"{parameter.Name}\"];");
                        }

                        sb.AppendLine("        }");
                        sb.AppendLine("");
                    }

                    if (parameters.Length > 0)
                    {
                        sb.AppendLine(
                            $"        public static ValueTask<{wrapperClassName}> BindAsync(HttpContext context, System.Reflection.ParameterInfo parameter)");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            return ValueTask.FromResult(new {wrapperClassName}());");
                        sb.AppendLine("        }");
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateDedicatedInvokers(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using Hubcon;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");

            sb.AppendLine($"    public static class EndpointInvokerProvider");
            sb.AppendLine("    {");
            sb.AppendLine("        public static IEndpointInvoker GetInvoker(string contractName, string signature)");
            sb.AppendLine("        {");
            sb.AppendLine("            var finalSignature = contractName + \"_\" + signature;");
            sb.AppendLine("            switch(finalSignature)");
            sb.AppendLine("            {");

            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);

                foreach (var method in methods)
                {
                    var invokerClassName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Invoker";
                    sb.AppendLine(
                        $"                 case \"{interfaceSymbol.Name}_{method.GetMethodSymbolSignature()}\": return new {invokerClassName}();");
                }
            }

            sb.AppendLine("                 default: return null;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();


            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);

                foreach (var method in methods)
                {
                    var invokerClassName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Invoker";
                    var wrapperTypeName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Request";
                    var controllerTypeName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    sb.AppendLine($"    public sealed class {invokerClassName} : IEndpointInvoker");
                    sb.AppendLine("    {");
                    sb.AppendLine("        public object Invoke(object target, object wrapper, CancellationToken ct)");
                    sb.AppendLine("        {");

                    // Casteo directo y local del target
                    sb.AppendLine($"            var typedTarget = ({controllerTypeName})target;");

                    // Chequeamos si tiene parámetros que requieran usar el wrapper
                    bool hasRealParameters = false;
                    foreach (var param in method.Parameters)
                    {
                        if (param.Type.ToDisplayString() != "System.Threading.CancellationToken")
                        {
                            hasRealParameters = true;
                            break;
                        }
                    }

                    // Solo casteamos el wrapper si realmente se espera que exista un objeto con datos
                    if (hasRealParameters)
                    {
                        sb.AppendLine(
                            $"            var typedWrapper = (global::Hubcon.Generated.{wrapperTypeName})wrapper;");
                    }

                    sb.AppendLine();

                    // Mapeo de parámetros
                    var paramList = new List<string>();
                    foreach (var param in method.Parameters)
                    {
                        if (param.Type.ToDisplayString() == "System.Threading.CancellationToken")
                        {
                            paramList.Add("ct");
                        }
                        else
                        {
                            // Si llegamos acá, sabemos que 'typedWrapper' fue declarado arriba
                            paramList.Add($"typedWrapper.{param.Name}");
                        }
                    }

                    string argsStr = string.Join(", ", paramList);

                    if (method.ReturnsVoid)
                    {
                        sb.AppendLine($"            typedTarget.{method.Name}({argsStr});");
                        sb.AppendLine("            return null;");
                    }
                    else
                    {
                        sb.AppendLine($"            return (object)typedTarget.{method.Name}({argsStr});");
                    }

                    sb.AppendLine("        }");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateHttpDelegates(IEnumerable<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using Hubcon;");
            sb.AppendLine();
            sb.AppendLine("namespace Hubcon.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class EndpointDelegateProvider");
            sb.AppendLine("    {");
            sb.AppendLine("        public static Delegate? GetDelegate(string contractName, string signature)");
            sb.AppendLine("        {");
            sb.AppendLine("            var finalSignature = contractName + \"_\" + signature;");
            sb.AppendLine("            switch(finalSignature)");
            sb.AppendLine("            {");

            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);

                foreach (var method in methods)
                {
                    var delegateName = $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Delegate";
                    sb.AppendLine(
                        $"                 case \"{interfaceSymbol.Name}_{method.GetMethodSymbolSignature()}\": return EndpointDelegateProvider.{delegateName};");
                }
            }

            sb.AppendLine("                 default: return null;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");


            foreach (var interfaceSymbol in interfaces)
            {
                var methods = interfaceSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary);

                foreach (var method in methods)
                {
                    var delegateName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Delegate";
                    var wrapperTypeName =
                        $"{interfaceSymbol.GetSafeName()}_{method.GetMethodSymbolSignature()}_Request";

                    if (method.Parameters.Count(x => x.Type.ToDisplayString() != "System.Threading.CancellationToken") == 0)
                    {
                        sb.AppendLine($"        public static {method.GetHubconResponseTypeFromMethod()} {delegateName}() => default!;");
                    }
                    else
                    {
                        sb.AppendLine($"        public static {method.GetHubconResponseTypeFromMethod()} {delegateName}([AsParameters] {wrapperTypeName} request) => default!;");
                    }
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}