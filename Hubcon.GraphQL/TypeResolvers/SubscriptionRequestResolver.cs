using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Subscriptions;

namespace Hubcon.GraphQL.TypeResolvers
{
    public class SubscriptionRequestResolver
    {
        public ISubscriptionRequest ResolveISubscriptionRequest(SubscriptionRequest request)
        {
            return request;
        }
    }
}
