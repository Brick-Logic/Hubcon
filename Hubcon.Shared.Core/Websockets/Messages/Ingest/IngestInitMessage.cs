using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public record class IngestInitMessage : BaseMessage
    {
        private Guid[]? _streamIds;
        private JsonElement? _payload;

        public IngestInitMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public IngestInitMessage(Guid id, Guid[] streamIds, JsonElement payload) : base(MessageType.ingest_init, id)
        {
            _streamIds = streamIds;
            _payload = payload;
        }

        public Guid[] StreamIds => _streamIds ??= Extract<Guid[]>("streamIds")!;
        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload")!;
    }
}
