using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Subscriptions;

namespace Hubcon.GraphQL.TypeResolvers
{
    public class SubscriptionRequestResolver
    {
        public IOperationRequest ResolveISubscriptionRequest(SubscriptionRequest request)
        {
            return request;
        }
    }
}
