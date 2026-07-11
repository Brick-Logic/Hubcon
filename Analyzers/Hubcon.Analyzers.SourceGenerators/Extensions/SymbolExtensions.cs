using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hubcon.Analyzers.SourceGenerators.Extensions
{
    public static class SymbolExtensions
    {
        /// <summary>
        /// Obtiene el ITypeSymbol de un tipo conocido por su nombre completo.
        /// </summary>
        public static INamedTypeSymbol GetTypeSymbol<T>(this Compilation compilation)
        {
            var type = typeof(T);

            // 1. Intento directo
            var symbol = compilation.GetTypeByMetadataName(type.FullName);
            if (symbol != null) return symbol;

            // 2. Intento exhaustivo en referencias
            foreach (var reference in compilation.References)
            {
                var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (assemblySymbol != null)
                {
                    var found = assemblySymbol.GetTypeByMetadataName(type.FullName);
                    if (found != null) return found;
                }
            }

            return null;
        }
        
        public static bool ImplementsControllerContract(this INamedTypeSymbol symbol)
        {
            if (symbol == null)
                return false;

            // Chequeamos que implemente IControllerContract
            return symbol.AllInterfaces
                .Any(i => i.Name == nameof(IControllerContract));
        }

        public static INamedTypeSymbol GetHubconResponseSymbol(Compilation compilation)
        {
            return compilation.GetTypeByMetadataName("Hubcon.HubconResponse`1");
        }
        
        public static bool IsIAsyncEnumerable(this ITypeSymbol type)
        {
            return type is INamedTypeSymbol namedType &&
                   namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IAsyncEnumerable<T>";
        }
        
        public static bool IsDictionary(this ITypeSymbol type, out ITypeSymbol keyType, out ITypeSymbol valueType)
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

        public static bool IsCollection(this ITypeSymbol type, out ITypeSymbol elementType)
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

        public static string GetSafeName(this ITypeSymbol type)
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
        
        public static void CollectAsyncTypesTo(this ITypeSymbol type, HashSet<ITypeSymbol> set)
        {
            if (!(type is INamedTypeSymbol named)) return;
            switch (named.Name)
            {
                case "IAsyncEnumerable" when named.IsGenericType:
                    set.Add(named.TypeArguments[0]);
                    break;
                case "Task" when named.IsGenericType && named.TypeArguments[0] is INamedTypeSymbol inner:
                {
                    if (inner.Name == "IAsyncEnumerable" && inner.IsGenericType)
                    {
                        set.Add(inner.TypeArguments[0]);
                    }

                    break;
                }
            }
        }
        
        public static string GetGenericArgument(this string fullTypeName, string genericTypeName)
        {
            int start = genericTypeName.Length + 1;
            int end = fullTypeName.LastIndexOf('>');
            if (start >= end || start < 0 || end < 0)
                return "System.Object"; // fallback

            return fullTypeName.Substring(start, end - start);
        }
        
        public static void CollectTypesRecursiveTo(this ITypeSymbol type, HashSet<ITypeSymbol> typesToSerialize, INamedTypeSymbol hubconResponseBaseSymbol)
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
                    arrayType.ElementType.CollectTypesRecursiveTo(typesToSerialize, hubconResponseBaseSymbol);
                }
                return;
            }

            if (type is INamedTypeSymbol named)
            {
                // 3. Caso especial Task<T>: Desempaquetar y salir (No queremos Task en el JSON)
                if (named.IsGenericType && named.Name == "Task" && ns == "System.Threading.Tasks")
                {
                    foreach (var arg in named.TypeArguments) arg.CollectTypesRecursiveTo(typesToSerialize, hubconResponseBaseSymbol);
                    return;
                }

                // 4. Agregar el tipo actual (sea List<User>, User, o int?)
                // Si ya estaba, cortamos para evitar bucles infinitos
                if (!typesToSerialize.Add(type)) return;

                // --- Generar HubconResponse<T> ---
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
                        arg.CollectTypesRecursiveTo(typesToSerialize, hubconResponseBaseSymbol);
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
                            prop.Type.CollectTypesRecursiveTo(typesToSerialize, hubconResponseBaseSymbol);
                        }
                    }
                }
            }
        }
        
        public static bool ImplementsOrInherits(INamedTypeSymbol typeSymbol, INamedTypeSymbol targetSymbol)
        {
            if (targetSymbol.TypeKind == TypeKind.Interface)
            {
                return typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, targetSymbol));
            }

            var current = typeSymbol.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, targetSymbol))
                    return true;
            
                current = current.BaseType;
            }

            return false;
        }
    }
}
