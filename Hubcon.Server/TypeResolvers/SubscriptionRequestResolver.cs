using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Subscriptions;

namespace Hubcon.Server.TypeResolvers
{
    public class SubscriptionRequestResolver
    {
        public IOperationRequest ResolveISubscriptionRequest(SubscriptionRequest request)
        {
            return request;
        }
    }
}
