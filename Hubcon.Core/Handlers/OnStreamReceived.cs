using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Core.Handlers
{
    public class OnStreamReceived : IOnStreamReceived
    {
        public event Func<ChannelReader<object>, Task> OnStreamReceivedEvent;

        public Delegate GetCurrentEvent() => OnStreamReceivedEvent;

        public async Task Notify(ChannelReader<object> enumerable) => await OnStreamReceivedEvent.Invoke(enumerable);
    }
}
