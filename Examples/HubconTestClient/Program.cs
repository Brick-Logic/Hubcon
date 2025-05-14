using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.GraphQL.Injection;
using Hubcon.Core.Builders;
using HubconTestDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace HubconTestClient
{
    internal class Program
    {
        private const string Url = "http://localhost:5000/clienthub";

        static async Task Main()
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSingleton<GraphQLHttpClient>(x =>
            {
                var configuration = x.GetRequiredService<IConfiguration>();
                var graphqlEndpoint = configuration["GraphQL:HttpEndpoint"] ?? "http://localhost:5000/graphql";
                var graphqlWebSocketEndpoint = configuration["GraphQL:WebSocketEndpoint"] ?? "ws://localhost:5000/graphql";

                var options = new GraphQLHttpClientOptions
                {
                    EndPoint = new Uri(graphqlEndpoint),
                    WebSocketEndPoint = new Uri(graphqlWebSocketEndpoint),
                    WebSocketProtocol = "graphql-transport-ws"
                };

                return new GraphQLHttpClient(options, new SystemTextJsonSerializer());
            });

            builder.AddHubconGraphQLClient();
            builder.UseContractsFromAssembly(nameof(HubconTestDomain));

            var app = builder.Build();


            var scope = app.Services.CreateScope();

            var clientProvider = scope.ServiceProvider.GetRequiredService<IHubconClientProvider>();

            var client = clientProvider.GetClient<ITestContract>();


            Console.WriteLine("Esperando interacción antes de continuar...");
            Console.ReadKey();

            Console.WriteLine("Conectando evento...");

            int eventosRecibidos = 0;

            async Task handler(int input)
            {
                Console.WriteLine($"Evento recibido: {input}");
                Interlocked.Add(ref eventosRecibidos, 1);
            }

            client.OnUserCreated!.AddHandler(handler);
            await client.OnUserCreated.Subscribe();
            await client.CreateUser();
            Console.WriteLine("Evento conectado.");

            Console.ReadKey();
            Console.WriteLine("Enviando request GetTemperatureFromServer...");
            var temp = await client.GetTemperatureFromServer();
            Console.WriteLine($"Datos recibidos: {temp}");
            Console.ReadKey();

            Console.WriteLine("Enviando request...");
            await client.CreateUser();
            Console.WriteLine($"Request enviado, respuesta recibida.");
            Console.ReadKey();

            Console.WriteLine("Enviando request...");
            await foreach (var item in client.GetMessages(10))
            {
                Console.WriteLine($"Respuesta recibida: {item}");
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
            var sw = new Stopwatch();
            var worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs)
                =>
            {
                var avgRequestsPerSec = finishedRequestsCount - lastRequests;
                var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
                Console.WriteLine($"Requests: {finishedRequestsCount} | Avg requests/s:{avgRequestsPerSec} | Received events: {eventosRecibidos} | Avg request time: {nanosecs / avgRequestsPerSec}");
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
