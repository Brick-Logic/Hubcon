using System.Collections.Generic;
using System.Linq;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;

namespace Hubcon.Analyzers.SourceGenerators
{
    public static class SymbolTools
    {
        public static void CollectInterfacesFromAssemblyTo(this IAssemblySymbol assemblySymbol, List<INamedTypeSymbol> interfaces, INamespaceSymbol nameSpace = null)
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
    }
}