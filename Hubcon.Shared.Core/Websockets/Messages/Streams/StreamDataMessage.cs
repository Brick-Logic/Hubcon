using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public record class StreamDataMessage(Guid Id, JsonElement Data) : BaseMessage(MessageType.stream_data, Id);
}
