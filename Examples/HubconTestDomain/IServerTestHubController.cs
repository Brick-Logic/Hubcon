using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Subscriptions;

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
        ISubscription? OnUserCreated { get; }

        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task CreateUser();
    }
}







