using Hubcon;

namespace TestDomain
{
    public interface ITestServerHubController : IServerHubController
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}
