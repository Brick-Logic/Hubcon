using System.Collections.Generic;
using System.Linq;
using Hubcon.Analyzers.SourceGenerators.Extensions;
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