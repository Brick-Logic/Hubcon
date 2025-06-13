using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public record class StreamDataWithAckMessage(string StreamId, JsonElement Data, string AckId) : BaseMessage(MessageType.stream_data_with_ack);
}
