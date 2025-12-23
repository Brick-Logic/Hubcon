using Hubcon.Shared.Abstractions.Interfaces;
using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface ILiveSubscriptionRegistry
    {
        ISubscriptionDescriptor? GetHandler(string clientId, string contractName, string subscriptionName);
        PropertyInfo? GetSubscriptionMetadata(string contractName, string descriptorSignature);
        ISubscriptionDescriptor RegisterHandler(string clientId, string contractName, string subscriptionName, ISubscription handler);
        void RegisterSubscriptionMetadata(string contractName, string descriptorSignature, PropertyInfo info);
        bool RemoveHandler(string clientId, string contractName, string subscriptionName);
    }
}
