using Hubcon.Core.Middleware;
using HubconTestDomain;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace HubconTestClient
{
    internal class Program
    {
        private const string Url = "http://localhost:5000/clienthub";

        static async Task Main()
        {
            var hubController = new TestHubController();
            var server = await hubController.StartInstanceAsync(Url, Console.WriteLine, null, options => 
            {
                options.AddMiddleware<LoggingMiddleware>();
            });

            var connector = server.GetConnector<IServerHubContract>();

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
                    int? response = await connector.GetTemperatureFromServer();

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
