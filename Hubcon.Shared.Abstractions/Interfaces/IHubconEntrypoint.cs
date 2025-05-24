using Hubcon.Shared.Abstractions.Models;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconEntrypoint : IBaseHubconController
    {
        public IAsyncEnumerable<JsonElement> HandleMethodStream(OperationRequest request);
        public void Build();
        IAsyncEnumerable<JsonElement> HandleSubscription(SubscriptionRequest request);
    }
}
