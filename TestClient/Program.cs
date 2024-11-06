using TestDomain;

namespace TestClient
{
    public static class Program
    {
#pragma warning disable S1075 // URIs should not be hardcoded
        private const string Url = "http://localhost:5237/clienthub";
#pragma warning restore S1075 // URIs should not be hardcoded

        static async Task Main()
        {
            var connector = new TestHubController(Url)
                .GetConnector<IServerTestHubController>();

            await connector.ShowTextOnServer();
            var serverData = await connector.GetTemperatureFromServer();
            await connector.ShowTempOnServerFromClient();

            Console.WriteLine(serverData);
            Console.ReadKey();
        }
    }
}
