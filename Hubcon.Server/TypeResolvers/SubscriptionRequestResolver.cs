using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;

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
