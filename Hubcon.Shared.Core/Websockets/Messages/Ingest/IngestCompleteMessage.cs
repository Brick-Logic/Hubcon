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
    public record class IngestCompleteMessage : BaseMessage
    {
        private Guid[]? _streamIds;

        public IngestCompleteMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public IngestCompleteMessage(Guid id, Guid[] streamIds) : base(MessageType.ingest_complete, id)
        {
            _streamIds = streamIds;
        }

        public Guid[] StreamIds => _streamIds ??= Extract<Guid[]>("streamIds");
    }

}
