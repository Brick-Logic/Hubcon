using HotChocolate.Types;
using Hubcon.Core.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Core.Abstractions.TypeBindings
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
