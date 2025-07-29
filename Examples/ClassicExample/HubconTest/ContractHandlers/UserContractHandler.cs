using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Xml.Linq;

namespace HubconTest.ContractHandlers
{
    public class UserContractHandler(ILogger<UserContractHandler> logger) : IUserContract
    {
        public ISubscription<int?>? OnUserCreated { get; }
        public ISubscription<int?>? OnUserCreated2 { get; }
        public ISubscription<int?>? OnUserCreated3 { get; }
        public ISubscription<int?>? OnUserCreated4 { get; }

        public Task CreateUser(CancellationToken cancellationToken)
        {
            OnUserCreated?.Emit(1);
            OnUserCreated2?.Emit(2);
            OnUserCreated3?.Emit(3);
            OnUserCreated4?.Emit(4);

            return Task.CompletedTask;
        }


        public async Task<int> GetTemperatureFromServer(CancellationToken cancellationToken) 
            => await Task.Run(() => new Random().Next(-10, 50));

        [StreamingSettings(1000)]
        public async IAsyncEnumerable<string> GetMessages(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return await Task.Run(() => "hola2");
            }
        }

        [StreamingSettings(0)]
        public async IAsyncEnumerable<string> GetMessages2(CancellationToken cancellationToken)
        {
            while(true)
            {
                yield return "hola2";
            }
        }

        public async Task PrintMessage(string message)
        {
            logger.LogInformation(message);
            await Task.CompletedTask;
        }

        public Task ShowTextOnServer()
        {
            logger.LogInformation("Mostrando texto");
            return Task.CompletedTask;
        }

        [IngestSettings(5000, BoundedChannelFullMode.Wait, 5)]
        public async Task<string> IngestMessages(
            IAsyncEnumerable<string> source,
            IAsyncEnumerable<string> source2,
            IAsyncEnumerable<string> source3,
            IAsyncEnumerable<string> source4,
            IAsyncEnumerable<string> source5)
        {
            Task TaskRunner<T>(IAsyncEnumerable<T> source, string name)
            {
                return Task.Run(async () =>
                {
                    await foreach (var item in source)
                    {
                        logger.LogInformation($"source1: {item}");
                    }
                    logger.LogInformation($"[{name}] Stream terminado.");
                });
            }

            List<Task> sources =
            [
                TaskRunner(source, nameof(source)),
                TaskRunner(source2, nameof(source2)),
                TaskRunner(source3, nameof(source3)),
                TaskRunner(source4, nameof(source4)),
                TaskRunner(source5, nameof(source5)),
            ];

            await Task.WhenAll(sources);
            logger.LogInformation("Ingest terminado exitosamente");

            return "Ok";
        }

        public async Task IngestMessages2(
            IAsyncEnumerable<string> source,
            IAsyncEnumerable<string> source2,
            IAsyncEnumerable<string> source3,
            IAsyncEnumerable<string> source4,
            IAsyncEnumerable<string> source5)
        {
            Task TaskRunner<T>(IAsyncEnumerable<T> source, string name)
            {
                return Task.Run(async () =>
                {
                    await foreach (var item in source)
                    {
                        logger.LogInformation($"source1: {item}");
                    }
                    logger.LogInformation($"[{name}] Stream terminado.");
                });
            }

            List<Task> sources =
            [
                TaskRunner(source, nameof(source)),
                TaskRunner(source2, nameof(source2)),
                TaskRunner(source3, nameof(source3)),
                TaskRunner(source4, nameof(source4)),
                TaskRunner(source5, nameof(source5)),
            ];

            await Task.WhenAll(sources);
            logger.LogInformation("Ingest terminado exitosamente");
        }


        public Task<MyTestClass> GetObject()
        {
            return Task.FromResult(new MyTestClass("hola", new TestClass2("propiedad")));
        }

        public async Task<IEnumerable<bool>> GetBooleans()
        {
            return Enumerable.Range(0, 5).Select(x => true);
        }

        private static Task? _monitor;
        private async Task Monitor(CancellationToken cancellationToken)
        {
            // Método auxiliar para calcular percentiles
            static double Percentile(double[] sortedData, double percentile)
            {
                if (sortedData == null || sortedData.Length == 0)
                    return 0;

                double position = (percentile / 100.0) * (sortedData.Length + 1);
                int index = (int)position;

                if (index < 1) return sortedData[0];
                if (index >= sortedData.Length) return sortedData[^1];

                double fraction = position - index;
                return sortedData[index - 1] + fraction * (sortedData[index] - sortedData[index - 1]);
            }

            sw = Stopwatch.StartNew();

            var worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    worker.Stop();

                var avgRequestsPerSec = finishedRequestsCount - lastRequests;

                double avgLatency = 0;
                double p50 = 0, p95 = 0, p99 = 0;

                var latenciesSnapshot = latencies.ToArray();
                latencies.Clear();

                if (latenciesSnapshot.Length > 0)
                {
                    Array.Sort(latenciesSnapshot);
                    avgLatency = latenciesSnapshot.Average();

                    p50 = Percentile(latenciesSnapshot, 50);
                    p95 = Percentile(latenciesSnapshot, 95);
                    p99 = Percentile(latenciesSnapshot, 99);
                }

                maxReqs = Math.Max(maxReqs, avgRequestsPerSec);

                logger.LogInformation($"Requests: {finishedRequestsCount} | Avg requests/s: {avgRequestsPerSec} | Max req/s: {maxReqs} | " +
                                      $"p50 latency(ms): {p50:F2} | p95 latency(ms): {p95:F2} | p99 latency(ms): {p99:F2} | Avg latency(ms): {avgLatency:F2}");

                var allocated = GC.GetTotalMemory(forceFullCollection: false);
                logger.LogInformation($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {sw.Elapsed}");

                lastRequests = finishedRequestsCount;
                sw.Restart();
            };
            worker.Start();
        }

        static ConcurrentBag<double> latencies = new();
        static int finishedRequestsCount = 0;
        static int lastRequests = 0;
        static int maxReqs = 0;
        static Stopwatch sw;

        [IngestSettings(100, BoundedChannelFullMode.Wait, 1)]
        public async Task IngestMessages(IAsyncEnumerable<string> source, CancellationToken cancellationToken)
        {
            _monitor ??= Monitor(cancellationToken);

            Stopwatch? swReq;

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                swReq = Stopwatch.StartNew();

                try
                {
                    Interlocked.Increment(ref finishedRequestsCount);
                }
                finally
                {
                    swReq.Stop();
                    latencies.Add(swReq.Elapsed.TotalMilliseconds);
                }
            }

            logger.LogInformation("Ingest terminado exitosamente");
        }
    }
}