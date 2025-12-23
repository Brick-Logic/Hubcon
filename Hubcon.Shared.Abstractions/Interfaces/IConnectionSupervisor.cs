using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IConnectionSupervisor
    {
        bool IsExpired(string Id);
        void Register(string id, DateTime expiration, Action cancellationCallback);
        Task UnregisterAsync(string id);
        void UpdateExpiration(string id, DateTime newExpiration);
    }
}
