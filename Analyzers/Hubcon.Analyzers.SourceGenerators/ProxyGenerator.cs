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
using Hubcon.Analyzers.SourceGenerators.GeneratorCommands;

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
            
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => SymbolTools.IsCandidateClass(node),
                    transform: (ctx, _) => SymbolTools.GetClassIfImplementsInterface(ctx))
                .Where(c => c != null)
                .Collect();
            
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
            var allModels = localInterfaces
                .Combine(referencedInterfaces)
                .Select((combined, _) =>
                {
                    var (local, referenced) = combined;
                    return local.Concat(referenced).Distinct(SymbolEqualityComparer.Default).ToArray();
                })
                .Combine(classDeclarations);

            var finalProvider = allModels
                .Combine(context.CompilationProvider.Select((c, _) => c.AssemblyName))
                .Combine(shouldExecute);

            context.RegisterSourceOutput(finalProvider, (spc, data) =>
            {
                var (((interfaceList, classesList), assemblyName), shouldGenerate) = data;

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
                        GenerateProxyClass.Execute(spc, iface, $"{safeHintName}Proxy.g.cs");
                    }
                }

                if (generateForClient)
                {
                    GenerateProxyRegistry.Execute(spc, interfaces, "ProxyLookup.g.cs");
                    GenerateEnumerableWrapper.Execute(spc, interfaces, "AsyncEnumerableWrapper.g.cs");
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
                    GenerateMetadataResolver.Execute(spc, resolverClassName, filteredTypes, $"{resolverClassName}.g.cs");
                    generatedResolverClasses.Add("Hubcon.Shared.Core.Serialization.SystemTypesContext");
                    
                    // Al final, generas el archivo global
                    if (generatedResolverClasses.Any())
                    {
                        GenerateGlobalTypeResolver.Execute(spc, generatedResolverClasses, "HubconGlobalSerialization.g.cs");
                    }
                }
                
                if (generateForServer)
                {
                    GenerateEndpointParameterWrappers.Execute(spc, interfaces, "EndpointParameterWrappers.g.cs");
                    GenerateDedicatedInvokers.Execute(spc, interfaces, "EndpointInvokers.g.cs");
                    GenerateHttpDelegates.Execute(spc, interfaces, "EndpointDelegates.g.cs");
                    
                    var controllers = classesList.OfType<INamedTypeSymbol>();
                    GenerateControllerPreservers.Execute(spc, controllers, "ControllerPreservers.g.cs");
                    GenerateControllerFactories.Execute(spc, controllers, "ControllerFactories.g.cs");
                    GenerateControllerTypeProvider.Execute(spc, controllers, "ControllerTypeProvider.g.cs");
                }
            });
        }
    }
}