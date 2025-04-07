using HubconTestDomain;

namespace HubconTestClient
{
    internal class Program
    {
        #pragma warning disable S1075 // URIs should not be hardcoded
        private const string Url = "http://localhost:5056/clienthub";
        #pragma warning restore S1075 // URIs should not be hardcoded

        static async Task Main()
        {

            var hubController = new TestHubController();

            //await hubController.StartAsync(Url, Console.WriteLine);

            var server = await hubController.StartInstanceAsync(Url, Console.WriteLine);

            var client = server.GetConnector<IServerHubContract>();

            var list = client.GetMessages(10);

            await foreach (var message in list)
            {
                Console.WriteLine(message);
            }

            Console.ReadKey();
        }
    }
}
