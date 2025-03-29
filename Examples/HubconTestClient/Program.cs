using Hubcon.SignalR.Models.Interfaces;

namespace HubconTestClient
{
    internal class Program
    {
        #pragma warning disable S1075 // URIs should not be hardcoded
        private const string Url = "http://localhost:5056/clienthub";
        #pragma warning restore S1075 // URIs should not be hardcoded

        static async Task Main()
        {

            var hubController = new TestHubController(Url);
            await hubController.StartAsync(Console.WriteLine);

            //var connector = hub.sERVER;

            //var message = "PING";
            //await connector.PrintMessage(message);

            Console.ReadKey();
        }
    }
}
