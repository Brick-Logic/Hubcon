using Hubcon.Controllers;
using TestDomain;

namespace TestClient
{
    public class TestHubController(string url) : ClientHubController<IServerTestHubController>(url), ITestHubController
    {
        public async Task ShowText() => await Task.Run(() => Console.WriteLine("ShowText() invoked succesfully."));
        public async Task<int> GetTemperature() => await Task.Run(() => new Random().Next(-10, 50));

        public async Task Random()
        {
            var temperatura = await Server.GetTemperatureFromServer();
            Console.WriteLine($"Temperatura desde el conector: {temperatura}");
        }
    }
}
