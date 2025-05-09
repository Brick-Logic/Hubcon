using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Subscriptions;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace Hubcon.Core.Invocation
{
    public interface IHubconEntrypoint : IBaseHubconController
    {
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest request);
        public void Build(WebApplication? app = null);
        IAsyncEnumerable<JsonElement?> HandleSubscription(SubscriptionRequest request);
    }
}
