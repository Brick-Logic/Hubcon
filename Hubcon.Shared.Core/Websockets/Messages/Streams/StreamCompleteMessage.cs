using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public record class StreamCompleteMessage(string Id) : BaseMessage(MessageType.stream_complete, Id);
}