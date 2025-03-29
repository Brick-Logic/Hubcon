using Hubcon.Core.Interfaces.Communication;

namespace HubconTestDomain
{
    public interface IServerHubContract : ICommunicationContract
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}
