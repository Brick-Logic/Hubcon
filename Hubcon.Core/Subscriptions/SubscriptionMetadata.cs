using Hubcon.Core.Abstractions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Subscriptions
{
    public class SubscriptionDescriptor : ISubscriptionDescriptor
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

            if (sourceProperty != null)
            {
                Authorizations = sourceProperty.GetCustomAttributes<AuthorizeAttribute>().ToList();

                if (Authorizations.Count > 0) 
                    NeedsAuthorization = true;
            }
        }
    }
}
