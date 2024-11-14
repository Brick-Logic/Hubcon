using Hubcon;
using TestDomain;

namespace TestAPI.HubControllers
{
    public class TestServerHubController : ServerHub<ITestClientController>, ITestServerHubController
    {
        public async Task<int> GetTemperatureFromServer() => await Task.Run(() => new Random().Next(-10, 50));
        public async Task ShowTextOnServer() => await Task.Run(() => Console.WriteLine("ShowTextOnServer() invoked succesfully."));
        public async Task ShowTempOnServerFromClient() => Console.WriteLine($"ShowTempOnServerFromClient: {await Client.GetTemperature()}");
    }
}
