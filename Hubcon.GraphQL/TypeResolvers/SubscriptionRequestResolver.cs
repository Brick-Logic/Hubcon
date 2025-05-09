using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.GraphQL.TypeResolvers
{
    public class SubscriptionRequestResolver
    {
        public ISubscriptionRequest ResolveISubscriptionRequest(ISubscriptionRequest request)
        {
            return request;
        }
    }
}
