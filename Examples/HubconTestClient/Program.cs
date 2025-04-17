using Hubcon.Core.Middleware;
using Hubcon.SignalR;
using HubconTestDomain;

namespace HubconTestClient
{
    internal class Program
    {
        private const string Url = "http://localhost:5000/clienthub";

        static async Task Main()
        {
            var hubController = new TestHubController();

            //await hubController.StartAsync(Url, Console.WriteLine);

            var server = await hubController.StartInstanceAsync(Url, Console.WriteLine, null, options => 
            {
                options.AddMiddleware<LoggingMiddleware>();
            });

            var client = server.GetConnector<IServerHubContract>();


            while (true)
            {

                //Console.ReadKey();
                //var list = client.GetMessages(10);

                //await foreach (var message in list)
                //{
                //    Console.WriteLine(message);
                //}


                Console.ReadKey();
                await client.ShowTextOnServer();

                Console.ReadKey();
                await client.ShowTempOnServerFromClient();

                Console.ReadKey();
                await client.ShowTextOnServer();
            }
        }
    }
}
