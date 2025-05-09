using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Core.Subscriptions
{
    public class SubscriptionRequest : ISubscriptionRequest
    {
        public string ContractName { get; }
        public string SubscriptionName { get; }

        public SubscriptionRequest(string subscriptionName, string contractName)
        {
            SubscriptionName = subscriptionName;
            ContractName = contractName;
        }
    }
}
