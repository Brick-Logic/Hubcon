using System.Collections.Concurrent;
using System.Net.Mime;
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Telemetry;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hubcon.Core.Telemetry;
using Hubcon.Shared.Abstractions.Models;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public class InternalTelemetryMiddleware : ITelemetryMiddleware
    {
        private readonly StripedCounter[] _flatCounters;
        private readonly int _numOperations;
        private readonly TelemetryChannelPipeline<IOperationBlueprint> _pipeline;
        private readonly OpenTelemetryBatchWorker _batchWorker;

        public InternalTelemetryMiddleware(ITelemetryProvider telemetryProvider)
        {
            _pipeline = new TelemetryChannelPipeline<IOperationBlueprint>();
            _batchWorker = new OpenTelemetryBatchWorker(_pipeline);

            var transportsCount = HubconTransportAttribute.GetTransportsCount();
            _numOperations = Enum.GetValuesAsUnderlyingType<OperationKind>().Length;

            _flatCounters = new StripedCounter[transportsCount * _numOperations];
            for (var i = 0; i < _flatCounters.Length; i++)
            {
                _flatCounters[i] = new StripedCounter();
            }

            var transports = HubconTransportAttribute.GetAllTransports().Values;

            _ = StartRpsTimer(transports);
            _ = _batchWorker.ExecuteAsync();

            async Task StartRpsTimer(IEnumerable<HubconTransportAttribute> transports)
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync())
                {
                    var dict = new Dictionary<HubconTransportAttribute, Snapshot>();
                    var operationsCount = Enum.GetValuesAsUnderlyingType<OperationKind>().Length;
                    foreach (var transport in transports)
                    {
                        var globalSlotId = transport.TelemetryId * operationsCount;

                        dict[transport] = new Snapshot()
                        {
                            Calls = _flatCounters[globalSlotId].GetAndReset(),
                            Invokes = _flatCounters[globalSlotId + 1].GetAndReset(),
                            StreamingsRequests = _flatCounters[globalSlotId + 2].GetAndReset(),
                            IngestsRequests = _flatCounters[globalSlotId + 3].GetAndReset()
                        };
                        
                    }
                    
                    var data = new RequestsPerSecondSnapshot(dict);
                    telemetryProvider.CallOnRequestsPerSecondUpdated(data);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementCounter(int transportId, int opIdx)
        {
            int index = (transportId * _numOperations) + opIdx;
            _flatCounters[index].Add(1);
        }

        /// <inheritdoc/>
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            var transportId = context.TransportType.TelemetryId;
            var opKind = (ushort)context.Blueprint.Kind;

            IncrementCounter(transportId, opKind);

            byte status = 0;

            try
            {
                await next();
            }
            catch (Exception ex)
            {
                status = 1; // 1 = Error
                context.Exception = ex;
            }
            finally
            {
                long elapsedTicks = Stopwatch.GetElapsedTime(startTimestamp).Ticks;
                
                var traceEvent = new TraceEvent<IOperationBlueprint>(
                    context.RequestId,
                    context.Blueprint,
                    context.TransportType,
                    startTimestamp,
                    elapsedTicks,
                    status,
                    context.Exception
                );

                _pipeline.Emit(in traceEvent);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct CounterSlot
        {
            [FieldOffset(0)] public long Value;
        }

        public class StripedCounter
        {
            private readonly CounterSlot[] _slots = new CounterSlot[Environment.ProcessorCount];

            public void Add(int count)
            {
                int slotIdx = Thread.GetCurrentProcessorId() % _slots.Length;
                Interlocked.Add(ref _slots[slotIdx].Value, count);
            }

            public long GetAndReset()
            {
                long total = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    total += Interlocked.Exchange(ref _slots[i].Value, 0);
                }

                return total;
            }
        }
    }
}