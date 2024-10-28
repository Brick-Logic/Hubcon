using Hubcon.Controller;
using TestDomain;

namespace TestClient
{
    public class TestHubController(string url) : HubController(url), ITestHubController
    {
        public async Task ShowText() => Console.WriteLine("ShowText() invoked succesfully.");
        public async Task<int> GetTemperature() => new Random().Next(-10, 50);
    }
}
