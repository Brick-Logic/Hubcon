using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.GraphQL.Client;
using Hubcon.GraphQL.Injection;
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

            builder.Services.AddSingleton(x =>
            {
                var configuration = x.GetRequiredService<IConfiguration>();
                var graphqlEndpoint = configuration["GraphQL:HttpEndpoint"] ?? "http://localhost:5000/graphql";
                var graphqlWebSocketEndpoint = configuration["GraphQL:WebSocketEndpoint"] ?? "ws://localhost:5000/graphql";

                var options = new GraphQLHttpClientOptions
                {
                    EndPoint = new Uri(graphqlEndpoint),
                    WebSocketEndPoint = new Uri(graphqlWebSocketEndpoint)
                };

                return new GraphQLHttpClient(options, new SystemTextJsonSerializer());
            });

            builder.AddHubconGraphQLClient();

            var app = builder.Build();
            var scope = app.Services.CreateScope();

            var clientProvider = scope.ServiceProvider.GetRequiredService<HubconClientProvider>();
            var client = clientProvider.GetClient<ITestContract>();

            Console.WriteLine("Esperando interacción antes de continuar...");
            Console.ReadKey();

            Console.WriteLine("Enviando request...");
            var temp = await client.GetTemperatureFromServer();
            Console.WriteLine($"Datos recibidos: {temp}");
            Console.ReadKey();
            
            Console.WriteLine("Enviando request...");
            await client.ShowTempOnServerFromClient();
            Console.WriteLine($"Request enviado, respuesta recibida.");
            Console.ReadKey();

            Console.WriteLine("Enviando request...");
            await foreach(var item in client.GetMessages(10))
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
                Console.WriteLine($"Requests: {finishedRequestsCount}. Avg requests/s:{avgRequestsPerSec}. Last request time: {nanosecs / avgRequestsPerSec}");
                lastRequests = finishedRequestsCount;
                sw.Restart();
            };

            worker.Start();
            sw.Start();

            while (true)
            {
                await Task.Run(async () =>
                {
                    int? response = await client.GetTemperatureFromServer();

                    if (response is null)
                        Interlocked.Add(ref errors, 1);
                    else
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
