using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface ISubscriptionRegistry
    {
        ISubscriptionDescriptor? GetHandler(string clientId, string contractName, string subscriptionName);
        PropertyInfo? GetSubscriptionMetadata(string contractName, string descriptorSignature);
        ISubscriptionDescriptor RegisterHandler(string clientId, string contractName, string subscriptionName, ISubscription handler);
        void RegisterSubscriptionMetadata(string contractName, string descriptorSignature, PropertyInfo info);
        bool RemoveHandler(string clientId, string contractName, string subscriptionName);
    }
}
