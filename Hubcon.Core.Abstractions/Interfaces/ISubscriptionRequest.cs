namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface ISubscriptionRequest
    {
        string ContractName { get; }
        string SubscriptionName { get; }
    }
}