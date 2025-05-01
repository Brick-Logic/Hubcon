namespace Hubcon.Core.Models
{
    public class SubscriptionRequest
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
