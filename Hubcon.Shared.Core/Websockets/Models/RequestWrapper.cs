using Hubcon.Shared.Core.Websockets.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Models
{
    internal sealed class RequestData : IRequest
    {
        public Guid Id { get; }

        public RequestType Type { get; }

        public JsonElement Request { get; }

        public RequestData(Guid id, JsonElement request, RequestType type)
        {
            Id = id;
            Request = request;
            Type = type;
        }
    }
}
