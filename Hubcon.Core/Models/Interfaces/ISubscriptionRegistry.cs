using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface ISubscriptionRegistry
    {
        ISubscription? GetHandler(string clientId, string contractName, string subscriptionName);
        void RegisterHandler(string clientId, string contractName, string subscriptionName, ISubscription handler);
        bool RemoveHandler(string clientId, string contractName, string subscriptionName);
    }
}
