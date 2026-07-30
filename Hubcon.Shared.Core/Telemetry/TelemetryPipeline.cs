using System.Runtime.CompilerServices;
using System.Threading;
using Hubcon.Shared.Abstractions.Models;
using System.Threading.Channels;

namespace Hubcon.Core.Telemetry
{
    public sealed class TelemetryChannelPipeline<T>
    {
        private readonly Channel<TraceEvent<T>> _channel;
        private long _droppedEvents;

        public ChannelReader<TraceEvent<T>> Reader => _channel.Reader;
        public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

        public TelemetryChannelPipeline(int capacity = 250_000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            };

            _channel = Channel.CreateBounded<TraceEvent<T>>(options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(in TraceEvent<T> traceEvent)
        {
            if (!_channel.Writer.TryWrite(traceEvent))
            {
                Interlocked.Increment(ref _droppedEvents);
            }
        }
    }
}