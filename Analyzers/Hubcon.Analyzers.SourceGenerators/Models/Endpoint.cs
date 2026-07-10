using System;
using System.Collections.Generic;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using HubconAnalyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;

namespace Hubcon.Analyzers.SourceGenerators.Models
{
    public class Endpoint
    {
        public string Name { get; }
        public string FullName { get; }
        public IMethodSymbol ControllerMethod { get; }
        public IMethodSymbol ContractMethod { get; }
        public INamedTypeSymbol Contract { get; }
        public HashSet<AttributeData> CombinedAttributes { get; }
        public IReadOnlyList<EndpointParameter> Parameters { get; }
        public string Identifier { get; }

        public Endpoint(string name, IMethodSymbol controllerMethod, IMethodSymbol contractMethod,
            HashSet<AttributeData> combinedAttributes)
        {
            var controller = controllerMethod.ContainingType;
            Contract = contractMethod.ContainingType;
            Identifier = $"{controller.Name}_{Contract.Name}_{controllerMethod.GetMethodSymbolSignature()}";
            
            Name = name;
            ControllerMethod = controllerMethod;
            ContractMethod = contractMethod;
            CombinedAttributes = combinedAttributes;
            FullName = $"{Contract.GetSafeName()}_{controller.Name}_{controllerMethod.GetMethodSymbolSignature()}";
            
            var parametersList = new List<EndpointParameter>();

            // Los métodos en C# garantizan tener el mismo orden y cantidad de parámetros 
            // entre la interfaz de contrato y su implementación física en el controlador.
            var contractParams = contractMethod.Parameters;
            var controllerParams = controllerMethod.Parameters;

            for (int i = 0; i < contractParams.Length; i++)
            {
                var contractParam = contractParams[i];
                var controllerParam = controllerParams[i];

                var paramName = contractParam.Name;

                var paramType = contractParam.Type;

                var paramCombinedAttributes = new HashSet<AttributeData>(AttributeTypeEqualityComparer.Instance);

                foreach (var attr in contractParam.GetAttributes()) paramCombinedAttributes.Add(attr);
                foreach (var attr in controllerParam.GetAttributes()) paramCombinedAttributes.Add(attr);

                parametersList.Add(new EndpointParameter(
                    paramName,
                    paramType,
                    paramCombinedAttributes,
                    controllerParam,
                    contractParam
                ));
            }

            Parameters = parametersList;
        }
    }
}