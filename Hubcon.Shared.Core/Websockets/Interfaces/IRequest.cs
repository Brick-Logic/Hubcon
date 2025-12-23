using System;
using System.Text.Json;

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
