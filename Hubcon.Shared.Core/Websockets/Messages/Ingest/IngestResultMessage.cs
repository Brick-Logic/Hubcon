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
    public sealed record class IngestResultMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestResultMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestResultMessage(Guid id, JsonElement data) : base(MessageType.ingest_result, id)
        {
            _data = data;
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
