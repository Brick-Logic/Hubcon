using Autofac;
using Hubcon.Core;
using Hubcon.Core.Connectors;
using Hubcon.Core.Models.Interfaces;
using Hubcon.SignalR.Client;
using HubconTest.Middleware.HubconMiddlewares;
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

            HubconServerConnector<IBaseHubconController<ICommunicationHandler>, ICommunicationHandler> serverConnector = new();

            var client = server.GetConnector<IServerHubContract>();


            while (true)
            {
                var list = client.GetMessages(10);

                await foreach (var message in list)
                {
                    Console.WriteLine(message);
                }

                //Console.ReadKey();

                //client.ShowTextOnServer();
                //Console.ReadKey();
                Console.ReadKey();

                await client.ShowTempOnServerFromClient();

                //client.ShowTextOnServer();
                //Console.ReadKey();
            }
        }
    }
}
