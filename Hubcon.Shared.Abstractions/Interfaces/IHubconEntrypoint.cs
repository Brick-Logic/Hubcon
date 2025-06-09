using Hubcon.Shared.Abstractions.Models;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconEntrypoint : IBaseHubconController
    {
        public Task<IAsyncEnumerable<object?>> HandleMethodStream(OperationRequest request);
        public void Build();
        public Task<IAsyncEnumerable<object?>> HandleSubscription(SubscriptionRequest request);
    }
}
