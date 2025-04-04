using Hubcon.Core.Interfaces.Communication;

namespace HubconTestDomain
{
    public interface IServerHubContract : ICommunicationContract
    {
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}







