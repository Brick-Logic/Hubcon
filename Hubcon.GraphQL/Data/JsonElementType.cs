using HotChocolate.Language;
using Hubcon.Core.Converters;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Data
{
    public class JsonScalarType : ScalarType<JsonElement?, StringValueNode>
    {
        private readonly IDynamicConverter _converter = new DynamicConverter();

        public JsonScalarType() : base("JsonScalarType") { }

        protected override JsonElement? ParseLiteral(StringValueNode literal)
        {
            try
            {
                // Usa DynamicConverter para serializar el literal de GraphQL
                return _converter.SerializeObject(literal.Value); // Deserializa el string a JsonElement
            }
            catch
            {
                // En caso de error, devuelve null o un valor predeterminado
                return null;
            }
        }

        protected override StringValueNode ParseValue(JsonElement? runtimeValue)
        {
            // Convierte JsonElement a string (JSON en texto)
            return new StringValueNode(runtimeValue?.GetRawText() ?? "{}");
        }

        public override IValueNode ParseResult(object? resultValue)
        {
            // Si el valor resultante es JsonElement?, se convierte a texto
            if (resultValue is JsonElement jsonElement)
            {
                return new StringValueNode(jsonElement.GetRawText() ?? "{}");
            }
            return new StringValueNode("{}"); // Valor predeterminado en caso de que no sea JsonElement?
        }

        public override string ToString()
        {
            return "JsonScalarType";
        }
    }

}
