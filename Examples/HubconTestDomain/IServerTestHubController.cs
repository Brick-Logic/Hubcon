using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface IServerHubContract : IControllerContract
    {
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }

    public interface ITestContract : IControllerContract
    {
        ISubscription<int>? OnUserCreated { get; }

        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task CreateUser();
    }
}







