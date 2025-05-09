using System.Reflection;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface ISubscriptionDescriptor : IDescriptor
    {
        PropertyInfo SourceProperty { get; }
        ISubscription Subscription { get; }
    }
}
