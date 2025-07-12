using Hubcon.Client;
using Hubcon.Shared.Core.Websockets.Interfaces;
using HubconTestClient.Auth;
using HubconTestClient.Modules;
using HubconTestDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace HubconTestClient
{
    internal class Program
    {
        private const string Url = "http://localhost:5000/clienthub";

        static async Task Main()
        {
            var process = Process.GetCurrentProcess();

            long coreMask = 0;
            for (int i = 0; i <= 0; i++)
            {
                coreMask |= 1L << i;
            }

            process.ProcessorAffinity = (IntPtr)coreMask;
            process.PriorityClass = ProcessPriorityClass.RealTime;


            var builder = WebApplication.CreateBuilder();

            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<TestModule>();
            builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

            var app = builder.Build();
            var scope = app.Services.CreateScope();

            var client = scope.ServiceProvider.GetRequiredService<IUserContract>();
            var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
            var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserContract>>();


            logger.LogInformation("Esperando interacción antes de continuar...");

            Console.ReadKey();

            //Console.WriteLine($"Iniciando ingest...");
            //IAsyncEnumerable<string> source1 = GetMessages(3);
            //IAsyncEnumerable<string> source2 = GetMessages(5);
            //IAsyncEnumerable<string> source3 = GetMessages(5);
            //IAsyncEnumerable<string> source4 = GetMessages(5);
            //IAsyncEnumerable<string> source5 = GetMessages(5);
            //await client.IngestMessages(source1, source2, source3, source4, source5);
            //Console.WriteLine($"Ingest terminado.");

            //Console.ReadKey();

            var result = await authManager.LoginAsync("miusuario", "");
            logger.LogInformation($"Login result: {result.IsSuccess}");

            Console.ReadKey();

            var text = await client2.TestReturn();


            logger.LogInformation($"TestVoid llamado. Texto recibido: {text}");
            Console.ReadKey();

            //logger.LogDebug("Conectando evento...");

            int eventosRecibidos = 0;

            //async Task handler(int? input)
            //{
            //    logger.LogInformation($"Evento recibido: {input}");
            //    Interlocked.Add(ref eventosRecibidos, 1);
            //}

            //client.OnUserCreated!.AddHandler(handler);
            //await client.OnUserCreated.Subscribe();
            //client.OnUserCreated2!.AddHandler(handler);
            //await client.OnUserCreated2.Subscribe();
            //client.OnUserCreated3!.AddHandler(handler);
            //await client.OnUserCreated3.Subscribe();
            //client.OnUserCreated4!.AddHandler(handler);
            //await client.OnUserCreated4.Subscribe();

            logger.LogInformation("Evento conectado.");

            Console.ReadKey();

            logger.LogInformation("Enviando request...");
            await client.CreateUser();
            logger.LogInformation($"Request terminado.");

            Console.ReadKey();

            logger.LogInformation("Enviando request GetTemperatureFromServer...");
            var temp = await client.GetTemperatureFromServer();
            logger.LogInformation($"Datos recibidos: {temp}");

            Console.ReadKey();

            logger.LogInformation("Enviando request...");

            await foreach (var item in client.GetMessages(10))
            {
                logger.LogInformation($"Respuesta recibida: {item}");
            }

            Console.ReadKey();

            //int finishedRequestsCount = 0;
            //int errors = 0;
            //int lastRequests = 0;
            //int maxReqs = 0;
            //var sw = new Stopwatch();
            //var worker = new System.Timers.Timer();
            //worker.Interval = 1000;
            //worker.Elapsed += (sender, eventArgs) =>
            //{
            //    var avgRequestsPerSec = finishedRequestsCount - lastRequests;
            //    var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
            //    maxReqs = maxReqs < avgRequestsPerSec ? avgRequestsPerSec : maxReqs;
            //    logger.LogInformation($"Requests: {finishedRequestsCount} | Avg requests/s:{avgRequestsPerSec} | Max req/s: {maxReqs}| Received events: {eventosRecibidos} | Avg request time: {nanosecs / avgRequestsPerSec}");
            //    lastRequests = finishedRequestsCount;
            //    sw.Restart();
            //    ThreadPool.GetAvailableThreads(out var workerThreads, out _);
            //    logger.LogInformation($"Threads disponibles: {workerThreads}");
            //};
            //worker.Start();

            //List<Task> tasks = Enumerable.Range(6, 6).Select(_ => Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await client.CreateUser();
            //        Interlocked.Add(ref finishedRequestsCount, 1);
            //    }
            //})).ToList();

            //tasks.AddRange(Enumerable.Range(5, 5).Select(_ => Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await client.CreateUser().ConfigureAwait(false);
            //        Interlocked.Add(ref finishedRequestsCount, 1);
            //    }
            //})).ToList());

            int finishedRequestsCount = 0;
            int errors = 0;
            int lastRequests = 0;
            int maxReqs = 0;
            var sw = Stopwatch.StartNew();

            // Thread-safe para almacenar latencias de requests en ms
            ConcurrentBag<double> latencies = new();

            var worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs) =>
            {
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

                logger.LogInformation($"Requests: {finishedRequestsCount} | Received Events: {eventosRecibidos} | Avg requests/s: {avgRequestsPerSec} | Max req/s: {maxReqs} | " +
                                      $"p50 latency(ms): {p50:F2} | p95 latency(ms): {p95:F2} | p99 latency(ms): {p99:F2} | Avg latency(ms): {avgLatency:F2}");

                lastRequests = finishedRequestsCount;
                sw.Restart();
            };
            worker.Start();

            List<Task> tasks = Enumerable.Range(0, 6).Select(a => Task.Run(async () =>
            {
                while(true)
                {
                    var swReq = Stopwatch.StartNew();
                    try
                    {
                        await client.CreateUser();
                        Interlocked.Increment(ref finishedRequestsCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                    finally
                    {
                        swReq.Stop();
                        latencies.Add(swReq.Elapsed.TotalMilliseconds);
                    }
                }
            })).ToList();

            await Task.WhenAll(tasks);
        }

        static async IAsyncEnumerable<string> GetMessages(int count)
        {
            for(int i = 0; i < count; i++)
            {
                var message = $"string:{i}";
                Console.WriteLine($"Enviando mensaje... [{message}]");
                yield return message;
                await Task.Delay(1000);
            }
        }

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
    }
}