using Hubcon.Client;
using Hubcon.Shared.Core.Websockets.Interfaces;
using HubconTestClient.Auth;
using HubconTestClient.Modules;
using HubconTestDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            var client = scope.ServiceProvider.GetRequiredService<IUserService>();
            var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
            var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserService>>();


            logger.LogInformation("Esperando interacción antes de continuar...");

            //Console.ReadKey();

            //Console.WriteLine($"Iniciando ingest...");
            //IAsyncEnumerable<string> source1 = GetMessages(15);
            //IAsyncEnumerable<string> source2 = GetMessages(10);
            //IAsyncEnumerable<string> source3 = GetMessages(15);
            //IAsyncEnumerable<string> source4 = GetMessages(20);
            //IAsyncEnumerable<string> source5 = GetMessages(25);
            //await client.IngestMessages(source1, source2, source3, source4, source5);
            //Console.WriteLine($"Ingest terminado.");

            Console.ReadKey();

            //var result = await authManager.LoginAsync("miusuario", "");
            //logger.LogInformation($"Login result: {result.IsSuccess}");

            //Console.ReadKey();

            //var text = client2.TestReturn();


            //logger.LogInformation($"TestVoid llamado. Texto recibido: {text}");
            //Console.ReadKey();

            //logger.LogDebug("Conectando evento...");

            int eventosRecibidos = 0;

            //async Task handler(int input)
            //{
            //    //logger.LogInformation($"Evento recibido: {input}");
            //    Interlocked.Add(ref eventosRecibidos, 1);
            //}

            ////client.OnUserCreated!.AddHandler(handler);
            ////await client.OnUserCreated.Subscribe();
            //logger.LogInformation("Evento conectado.");

            //Console.ReadKey();

            //logger.LogInformation("Enviando request...");
            //await client.CreateUser();
            //logger.LogInformation($"Request terminado.");

            //Console.ReadKey();

            //logger.LogInformation("Enviando request GetTemperatureFromServer...");
            //var temp = await client.GetTemperatureFromServer();
            //logger.LogInformation($"Datos recibidos: {temp}");

            //Console.ReadKey();

            //logger.LogInformation("Enviando request...");

            //await foreach (var item in client.GetMessages(10))
            //{
            //    logger.LogInformation($"Respuesta recibida: {item}");
            //}

            Console.ReadKey();

            int finishedRequestsCount = 0;
            int errors = 0;
            int lastRequests = 0;
            int maxReqs = 0;
            var sw = new Stopwatch();
            var worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs) =>
            {
                var avgRequestsPerSec = finishedRequestsCount - lastRequests;
                var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
                maxReqs = maxReqs < avgRequestsPerSec ? avgRequestsPerSec : maxReqs;
                logger.LogInformation($"Requests: {finishedRequestsCount} | Avg requests/s:{avgRequestsPerSec} | Max req/s: {maxReqs}| Received events: {eventosRecibidos} | Avg request time: {nanosecs / avgRequestsPerSec}");
                lastRequests = finishedRequestsCount;
                sw.Restart();
                ThreadPool.GetAvailableThreads(out var workerThreads, out _);
                logger.LogInformation($"Threads disponibles: {workerThreads}");
            };
            worker.Start();

            //int i = 0;

            //while (i < 10000)
            //{
            //    await client.CreateUser();
            //    i++;
            //}

            List<Task> tasks = Enumerable.Range(6, 6).Select(_ => Task.Run(async () =>
            {
                while (true)
                {
                    await client.CreateUser();
                    Interlocked.Add(ref finishedRequestsCount, 1);
                }
            })).ToList();

            //tasks.AddRange(Enumerable.Range(5, 5).Select(_ => Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await client.CreateUser().ConfigureAwait(false);
            //        Interlocked.Add(ref finishedRequestsCount, 1);
            //    }
            //})).ToList());

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
    }
}