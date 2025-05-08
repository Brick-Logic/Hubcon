using System.Reflection;

namespace Hubcon.Core.Models.Interfaces
{
    public interface ISubscriptionDescriptor : IDescriptor
    {
        PropertyInfo SourceProperty { get; }
        ISubscription Subscription { get; }
    }
}
