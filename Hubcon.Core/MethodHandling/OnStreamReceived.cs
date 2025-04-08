using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Core.MethodHandling
{
    public class OnStreamReceived : IOnStreamReceived
    {
        public event Func<ChannelReader<object>, Task>? OnStreamReceivedEvent;

        public Delegate? GetCurrentEvent() => OnStreamReceivedEvent;

        public void Notify(ChannelReader<object> enumerable) => _ = OnStreamReceivedEvent?.Invoke(enumerable);
    }
}
