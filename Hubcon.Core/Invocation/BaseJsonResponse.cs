using Hubcon.Core.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Core.Invocation
{
    public class BaseJsonResponse : BaseOperationResponse<JsonElement>, IOperationResponse<JsonElement>
    {
        public BaseJsonResponse() : base(true, default, null)
        {

        }

        public BaseJsonResponse(bool success, JsonElement data = default, string? error = null) : base(success, data, error)
        {

        }
    }
}
