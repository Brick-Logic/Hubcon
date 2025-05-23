namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ISubscriptionRequest
    {
        string ContractName { get; }
        string SubscriptionName { get; }
    }
}