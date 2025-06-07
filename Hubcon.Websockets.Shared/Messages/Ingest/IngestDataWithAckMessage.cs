using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Ingest
{
    public record class IngestDataWithAckMessage(string Id, JsonElement Data) : BaseMessage(MessageType.ingest_data_with_ack);
}
