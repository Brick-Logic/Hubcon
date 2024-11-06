using Hubcon;

namespace TestDomain
{
    public interface IServerTestHubController : IServerHubController
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}
