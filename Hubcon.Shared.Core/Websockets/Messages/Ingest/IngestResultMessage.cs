using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public sealed record class IngestResultMessage(Guid Id, JsonElement Data) : BaseMessage(MessageType.ingest_result, Id);
}
