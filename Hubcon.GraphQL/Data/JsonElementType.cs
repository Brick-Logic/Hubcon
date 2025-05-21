using HotChocolate.Language;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Serialization;
using System.Text.Json;

namespace Hubcon.GraphQL.Data
{
    public class JsonScalarType : ScalarType<JsonElement, StringValueNode>
    {
        private readonly IDynamicConverter _converter = new DynamicConverter();

        public JsonScalarType() : base("JsonScalarType") { }

        protected override JsonElement ParseLiteral(StringValueNode literal)
        {
            try
            {
                return _converter.SerializeObject(literal.Value);
            }
            catch
            {
                return default;
            }
        }

        protected override StringValueNode ParseValue(JsonElement runtimeValue)
        {
            return new StringValueNode(
                runtimeValue.ValueKind == JsonValueKind.Null || runtimeValue.ValueKind == JsonValueKind.Undefined 
                ? "{}" 
                : runtimeValue.GetRawText()
            );
        }

        public override IValueNode ParseResult(object? resultValue)
        {
            if (resultValue is JsonElement jsonElement)
            {
                return new StringValueNode(jsonElement.GetRawText() ?? "{}");
            }

            return new StringValueNode("{}");
        }

        public override string ToString()
        {
            return "JsonScalarType";
        }
    }

}
