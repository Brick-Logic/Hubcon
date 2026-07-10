using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Hubcon.Analyzers.SourceGenerators.Extensions;

namespace Hubcon.Analyzers.SourceGenerators.Models
{
    public class ControllerMetadata
    {
        public INamedTypeSymbol Controller { get; }
        public IReadOnlyList<INamedTypeSymbol> Contracts { get; }
        public IReadOnlyList<Endpoint> Endpoints { get; }

        public ControllerMetadata(INamedTypeSymbol controller)
        {
            Controller = controller;
            Contracts = controller.AllInterfaces.Where(x => x.ImplementsControllerContract()).ToList();

            var endpointsList = new List<Endpoint>();

            foreach (var contract in Contracts)
            {
                foreach (var member in contract.GetMembers())
                {
                    if (member is IMethodSymbol contractMethod && contractMethod.MethodKind == MethodKind.Ordinary)
                    {
                        var controllerMethod =
                            controller.FindImplementationForInterfaceMember(contractMethod) as IMethodSymbol;

                        if (controllerMethod == null) continue;

                        string endpointName = contractMethod.Name;

                        var combinedAttributes = new HashSet<AttributeData>(AttributeTypeEqualityComparer.Instance);

                        foreach (var attr in contractMethod.GetAttributes()) combinedAttributes.Add(attr);
                        foreach (var attr in controllerMethod.GetAttributes()) combinedAttributes.Add(attr);

                        endpointsList.Add(new Endpoint(endpointName, controllerMethod, contractMethod, combinedAttributes));
                    }
                }
            }

            Endpoints = endpointsList;
        }
    }

// --- Comparador utilitario para el HashSet ---
// Evita que si se repite exactamente el mismo atributo en la interfaz y en el controller, se duplique en el .g.cs
    internal class AttributeTypeEqualityComparer : IEqualityComparer<AttributeData>
    {
        public static readonly AttributeTypeEqualityComparer Instance = new AttributeTypeEqualityComparer();

        public bool Equals(AttributeData x, AttributeData y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;

            return SymbolEqualityComparer.Default.Equals(x.AttributeClass, y.AttributeClass);
        }

        public int GetHashCode(AttributeData obj)
        {
            return obj.AttributeClass != null
                ? SymbolEqualityComparer.Default.GetHashCode(obj.AttributeClass)
                : 0;
        }
    }
}