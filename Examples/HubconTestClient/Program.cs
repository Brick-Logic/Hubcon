using Hubcon.Client;
using Hubcon.Core.Builders;
using HubconTestClient.Modules;
using HubconTestDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HubconTestClient
{
    internal class Program
    {
        private const string Url = "http://localhost:5000/clienthub";

        static async Task Main()
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddHubconClient();
            builder.AddRemoteServerModule<TestModule>();

            var app = builder.Build();
            var scope = app.Services.CreateScope();

            var client = scope.ServiceProvider.GetRequiredService<ITestContract>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ITestContract>>();


            logger.LogInformation("Esperando interacción antes de continuar...");
            Console.ReadKey();
            logger.LogDebug("Conectando evento...");

            int eventosRecibidos = 0;

            async Task handler(int input)
            {
                logger.LogInformation($"Evento recibido: {input}");
                Interlocked.Add(ref eventosRecibidos, 1);
            }

            client.OnUserCreated!.AddHandler(handler);
            //await client.OnUserCreated.Subscribe();
            await client.CreateUser();
            logger.LogInformation("Evento conectado.");

            Console.ReadKey();
            logger.LogInformation("Enviando request GetTemperatureFromServer...");
            var temp = await client.GetTemperatureFromServer();
            logger.LogInformation($"Datos recibidos: {temp}");
            Console.ReadKey();

            logger.LogInformation("Enviando request...");
            await client.CreateUser();
            logger.LogInformation($"Request enviado, respuesta recibida.");
            Console.ReadKey();

            logger.LogInformation("Enviando request...");

            await foreach (var item in client.GetMessages(10))
            {
                logger.LogInformation($"Respuesta recibida: {item}");
            }

            Console.ReadKey();



            //TestHubController? hubController = new TestHubController();
            //var server = await hubController.StartInstanceAsync(Url, Console.WriteLine, null, options => 
            //{
            //    options.AddMiddleware<LoggingMiddleware>();
            //});

            //var connector = hubController.GetConnector<IServerHubContract>();

            //Console.WriteLine("Running test: ShowTextOnServer... ");
            //await connector.ShowTextOnServer();
            //Console.Write($"Done.");

            //Console.WriteLine("Running test: GetTemperatureFromServer... ");
            //var result3 = await connector.GetTemperatureFromServer();
            //Console.Write($"Done.");

            //Console.WriteLine("Running test: GetTemperatureFromServer... ");
            //await connector.ShowTempOnServerFromClient();
            //Console.Write($"Done.");

            //Console.WriteLine("Running test: GetMessages->IAsyncEnumerable<string>... ");
            //var result1 = connector.GetMessages(10).ToBlockingEnumerable();
            //Console.Write($"Done. MessageCount: {result1.Count()}");

            //Console.ReadKey();

            int finishedRequestsCount = 0;
            int errors = 0;
            int lastRequests = 0;
            int maxReqs = 0; 
            var sw = new Stopwatch();
            var worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs)
                =>
            {
                var avgRequestsPerSec = finishedRequestsCount - lastRequests;
                var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
                maxReqs = maxReqs < avgRequestsPerSec ? avgRequestsPerSec : maxReqs;
                logger.LogInformation($"Requests: {finishedRequestsCount} | Avg requests/s:{avgRequestsPerSec} | Max req/s: {maxReqs}| Received events: {eventosRecibidos} | Avg request time: {nanosecs / avgRequestsPerSec}");
                lastRequests = finishedRequestsCount;
                sw.Restart();
            };

            worker.Start();
            sw.Start();

            while (true)
            {
                await Task.Run(async () =>
                {
                    await client.CreateUser();
                    //await Task.Delay(1000);

                    Interlocked.Add(ref finishedRequestsCount, 1);
                });
            }




            //while (true)
            //{

            //    //Console.ReadKey();
            //    //var list = client.GetMessages(10);

            //    //await foreach (var message in list)
            //    //{
            //    //    Console.WriteLine(message);
            //    //}


            //    Console.ReadKey();
            //    await client.ShowTextOnServer();

            //    Console.ReadKey();
            //    await client.ShowTempOnServerFromClient();

            //    Console.ReadKey();
            //    await client.ShowTextOnServer();
            //}
        }
    }
}
