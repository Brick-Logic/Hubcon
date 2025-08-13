using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Interfaces
{
    public enum RequestType
    {
        Stream,
        Subscription,
        Ingest
    }

    public interface IRequest
    {
        public Guid Id { get; }
        public RequestType Type { get; }
        public JsonElement Request { get; }
    }
}
