using Hubcon.Shared.Abstractions.Interfaces;
using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface ISubscriptionDescriptor : IDescriptor
    {
        PropertyInfo SourceProperty { get; }
        ISubscription Subscription { get; }
    }
}
