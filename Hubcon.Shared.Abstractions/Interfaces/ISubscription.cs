using Hubcon.Shared.Abstractions.Enums;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Hubcon
{
    public interface ISubscription
    {
    }

    public interface ISubscription<T> : ISubscription
    {
    }
}