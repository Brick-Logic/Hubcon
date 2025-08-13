using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Hubcon.Server.Core.Subscriptions
{
    internal sealed class SubscriptionDescriptor : ISubscriptionDescriptor
    {
        public PropertyInfo SourceProperty { get; }
        public ISubscription Subscription { get; }
        public string DescriptorSignature { get; }
        public string ContractName { get; }
        public List<AuthorizeAttribute> Authorizations { get; } = new();
        public bool NeedsAuthorization { get; }

        public SubscriptionDescriptor(string contractName, PropertyInfo sourceProperty, ISubscription subscription)
        {
            SourceProperty = sourceProperty;
            Subscription = subscription;

            DescriptorSignature = sourceProperty.Name;
            ContractName = contractName;
            
            Authorizations = sourceProperty.GetCustomAttributes<AuthorizeAttribute>().ToList();

            if (Authorizations.Count > 0) 
                NeedsAuthorization = true;      
        }
    }
}
