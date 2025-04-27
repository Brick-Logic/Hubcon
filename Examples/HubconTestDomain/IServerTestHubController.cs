using Hubcon.Core.Models.Interfaces;
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
        ISubscriptionHandler<int>? OnEventCreated { get; }

        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}







