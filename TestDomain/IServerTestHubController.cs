using Hubcon.Models.Interfaces;

namespace TestDomain
{
    public interface IServerTestHubController : IServerHubController
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}
