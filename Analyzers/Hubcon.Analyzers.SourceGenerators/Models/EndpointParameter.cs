namespace Hubcon.Analyzers.SourceGenerators.Models
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    public class EndpointParameter
    {
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public string TypeFullName { get; }
        public string Namespace { get; }
        public HashSet<AttributeData> Attributes { get; }
        public IParameterSymbol ControllerParameter { get; }
        public IParameterSymbol ContractParameter { get; }

        public bool HasExplicitDefaultValue => ContractParameter.HasExplicitDefaultValue || ControllerParameter.HasExplicitDefaultValue;

        public object ExplicitDefaultValue
        {
            get
            {
                if (ContractParameter.HasExplicitDefaultValue)
                {
                    return ContractParameter.ExplicitDefaultValue;
                }
                else if (ControllerParameter.HasExplicitDefaultValue)
                {
                    return ControllerParameter.ExplicitDefaultValue;
                }

                throw new InvalidOperationException("Cannot read the explicit default value as there is no default value.");
            }
        }

        public EndpointParameter(
            string name, 
            ITypeSymbol type, 
            HashSet<AttributeData> combinedAttributes,
            IParameterSymbol controllerParameter,
            IParameterSymbol contractParameter)
        {
            Name = name;
            Type = type;
            TypeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            Namespace = type.ContainingNamespace?.ToDisplayString();
            Attributes = combinedAttributes;
            ControllerParameter = controllerParameter;
            ContractParameter = contractParameter;
        }
    }
}