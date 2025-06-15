using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
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

    public interface IUserService : IControllerContract
    {
        ISubscription<int>? OnUserCreated { get; }
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task CreateUser();
        Task IngestMessages(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2);
    }
}







