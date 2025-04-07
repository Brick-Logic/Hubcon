using Hubcon.Core.Models.Interfaces;

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







