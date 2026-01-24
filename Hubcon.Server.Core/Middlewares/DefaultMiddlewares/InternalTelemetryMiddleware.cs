using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Telemetry;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [StructLayout(LayoutKind.Explicit, Size = 64)] // Evita False Sharing
    public struct CounterSlot
    {
        [FieldOffset(0)] public long Value;
    }

    public class StripedCounter
    {
        private readonly CounterSlot[] _slots = new CounterSlot[Environment.ProcessorCount];

        // Incrementar es ultra rápido: cada hilo a su slot
        public void Add(int count)
        {
            int slotIdx = Thread.GetCurrentProcessorId() % _slots.Length;
            Interlocked.Add(ref _slots[slotIdx].Value, count);
        }

        // El "Reset" ocurre aquí: extraemos el valor y ponemos a cero en un paso atómico
        public long GetAndReset()
        {
            long total = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                // Interlocked.Exchange extrae el valor actual y pone 0 de forma atómica
                total += Interlocked.Exchange(ref _slots[i].Value, 0);
            }
            return total;
        }
    }

    public class InternalTelemetryMiddleware : ITelemetryMiddleware
    {
        private int currentSubscriptionCount = 0;
        private int currentStreamingsCount = 0;
        private int currentIngestCount = 0;

        private int currentWebSocketsRequestsCount = 0;

        private int currentWebSocketsCallRequestsCount = 0;
        private int currentWebSocketsRoundTripRequestsCount = 0;

        private int currentHttpRequestsCount = 0;

        private int currentHttpCallRequestsCount = 0;
        private int currentHttpRoundTripRequestsCount = 0;

        private int currentRequestsPerSecond = 0;
        private int currentHttpRequestsPerSecond = 0;
        private int requestAccumulator = 0;

        private int currentSubscriptionPerSecond = 0;
        private int currentStreamingsPerSecond = 0;
        private int currentIngestPerSecond = 0;
        private int currentWebSocketsRequestsPerSecond = 0;
        private int currentWebSocketsCallRequestsPerSecond = 0;
        private int currentWebSocketsRoundTripRequestsPerSecond = 0;
        private int currentHttpCallRequestsPerSecond = 0;
        private int currentHttpRoundTripRequestsPerSecond = 0;

        public InternalTelemetryMiddleware(ITelemetryProvider telemetryProvider)
        {
            _matrix = new StripedCounter[5, 2];

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    _matrix[i, j] = new StripedCounter();
                }
            }

            telemetryProvider.RegisterProvider(x => x.CurrentSubscriptionCount, () => currentSubscriptionCount);
            telemetryProvider.RegisterProvider(x => x.CurrentStreamingsCount, () => currentStreamingsCount);
            telemetryProvider.RegisterProvider(x => x.CurrentIngestCount, () => currentIngestCount);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsRequestsCount, () => currentWebSocketsRequestsCount);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsCallRequestsCount, () => currentWebSocketsCallRequestsCount);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsRoundTripRequestsCount, () => currentWebSocketsRoundTripRequestsCount);
            telemetryProvider.RegisterProvider(x => x.CurrentHttpCallRequestsCount, () => currentHttpCallRequestsCount);
            telemetryProvider.RegisterProvider(x => x.CurrentHttpRoundTripRequestsCount, () => currentHttpRoundTripRequestsCount);

            telemetryProvider.RegisterProvider(x => x.CurrentRequestsPerSecond, () => currentRequestsPerSecond);

            telemetryProvider.RegisterProvider(x => x.CurrentSubscriptionPerSecond, () => currentSubscriptionPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentStreamingsPerSecond, () => currentStreamingsPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentIngestPerSecond, () => currentIngestPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsRequestsPerSecond, () => currentWebSocketsRequestsPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsCallRequestsPerSecond, () => currentWebSocketsCallRequestsPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentWebSocketsRoundTripRequestsPerSecond, () => currentWebSocketsRoundTripRequestsPerSecond);


            telemetryProvider.RegisterProvider(x => x.CurrentHttpRequestsPerSecond, () => currentHttpRequestsPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentHttpCallRequestsPerSecond, () => currentHttpCallRequestsPerSecond);
            telemetryProvider.RegisterProvider(x => x.CurrentHttpRoundTripRequestsPerSecond, () => currentHttpRoundTripRequestsPerSecond);

            _ = StartRpsTimer();

            async Task StartRpsTimer()
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

                while (await timer.WaitForNextTickAsync())
                {
                    currentHttpCallRequestsPerSecond = (int)_matrix[0, 0].GetAndReset();
                    currentHttpRoundTripRequestsPerSecond = (int)_matrix[1, 0].GetAndReset();
                    currentHttpRequestsPerSecond = currentHttpCallRequestsPerSecond + currentHttpRoundTripRequestsPerSecond;

                    currentWebSocketsCallRequestsPerSecond = (int)_matrix[0, 1].GetAndReset();
                    currentWebSocketsRoundTripRequestsPerSecond = (int)_matrix[1, 1].GetAndReset();
                    currentSubscriptionPerSecond = (int)_matrix[2, 1].GetAndReset();
                    currentStreamingsPerSecond = (int)_matrix[3, 1].GetAndReset();
                    currentIngestPerSecond = (int)_matrix[4, 1].GetAndReset();
                    currentWebSocketsRequestsPerSecond = currentWebSocketsCallRequestsPerSecond + currentWebSocketsRoundTripRequestsPerSecond + currentSubscriptionPerSecond + currentStreamingsPerSecond + currentIngestPerSecond;

                    currentRequestsPerSecond = currentHttpRequestsPerSecond + currentWebSocketsRequestsPerSecond;

                    var data = new RequestsPerSecondSnapshot()
                    {
                        SubscriptionsPerSecond = currentSubscriptionPerSecond,
                        StreamingsPerSecond = currentStreamingsPerSecond,
                        IngestsPerSecond = currentIngestPerSecond,
                        WebSocketsRequestsPerSecond = currentWebSocketsRequestsPerSecond,
                        WebSocketsCallRequestsPerSecond = currentWebSocketsCallRequestsPerSecond,
                        WebSocketsRoundTripRequestsPerSecond = currentWebSocketsRoundTripRequestsPerSecond,
                        HttpRequestsPerSecond = currentHttpRequestsPerSecond,
                        HttpCallRequestsPerSecond = currentHttpCallRequestsPerSecond,
                        HttpRoundTripRequestsPerSecond = currentHttpRoundTripRequestsPerSecond,
                        RequestsPerSecond = currentRequestsPerSecond,
                    };

                    telemetryProvider.CallOnRequestsPerSecondUpdated(data);
                }
            }
        }



        private readonly StripedCounter[,] _matrix;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ChangeCounts(int count, IOperationContext context)
        {
            int protocolIdx = Unsafe.As<bool, byte>(ref Unsafe.AsRef(context.HttpContext.WebSockets.IsWebSocketRequest));
            int opIdx = (int)context.Blueprint.Kind;
            _matrix[opIdx, protocolIdx].Add(count);
            //if (context.HttpContext!.WebSockets.IsWebSocketRequest)
            //    Interlocked.Add(ref currentWebSocketsRequestsCount, count);
            //else
            //    Interlocked.Add(ref currentHttpRequestsCount, count);

            //switch (context.Blueprint.Kind)
            //{
            //    case OperationKind.Subscription:
            //        Interlocked.Add(ref currentSubscriptionCount, count);
            //        break;
            //    case OperationKind.Ingest:
            //        Interlocked.Add(ref currentIngestCount, count);
            //        break;
            //    case OperationKind.CallMethod:
            //        if (context.HttpContext!.WebSockets.IsWebSocketRequest)
            //            Interlocked.Add(ref currentWebSocketsCallRequestsCount, count);
            //        else
            //            Interlocked.Add(ref currentHttpCallRequestsCount, count);
            //        break;
            //    case OperationKind.InvokeMethod:
            //        if (context.HttpContext!.WebSockets.IsWebSocketRequest)
            //            Interlocked.Add(ref currentWebSocketsRoundTripRequestsCount, count);
            //        else
            //            Interlocked.Add(ref currentHttpRoundTripRequestsCount, count);
            //        break;
            //    case OperationKind.Stream:
            //        Interlocked.Add(ref currentStreamingsCount, count);
            //        break;
            //}
        }

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            ChangeCounts(1, context);
            await next();
        }
    }
}
