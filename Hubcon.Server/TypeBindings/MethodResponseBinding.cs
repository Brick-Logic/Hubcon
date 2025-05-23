using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Server.TypeBindings
{
    public class MethodResponseBinding : ObjectType<IOperationResponse<JsonElement>>
    {
        protected override void Configure(IObjectTypeDescriptor<IOperationResponse<JsonElement>> descriptor)
        {
            descriptor.Name("IMethodResponse");
            descriptor.Field(x => x.Success).Type<BooleanType>();
            descriptor.Field(x => x.Data).Type<AnyType>();
            descriptor.Field(x => x.Error).Type<StringType>();
        }
    }
}
