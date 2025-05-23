using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Subscriptions;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace Hubcon.Shared.Core.Invocation
{
    public interface IHubconEntrypoint : IBaseHubconController
    {
        public IAsyncEnumerable<JsonElement> HandleMethodStream(OperationRequest request);
        public void Build(WebApplication? app = null);
        IAsyncEnumerable<JsonElement> HandleSubscription(SubscriptionRequest request);
    }
}
