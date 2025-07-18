using Hubcon.Server.Abstractions.Interfaces;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Routing.MethodHandling
{
    internal sealed class OnStreamReceived : IOnStreamReceived
    {
        public event Func<ChannelReader<object>, Task>? OnStreamReceivedEvent;

        public Delegate? GetCurrentEvent() => OnStreamReceivedEvent;

        public void Notify(ChannelReader<object> enumerable) => _ = OnStreamReceivedEvent?.Invoke(enumerable);
    }
}
