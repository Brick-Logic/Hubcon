using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IRateLimiterManager
    {
        ValueTask DisposeAsync();
        ValueTask Link(Guid id, IOperationEndpoint endpoint);
        ValueTask<bool> TryAcquireAsync(MessageType type, IOperationEndpoint? operation = null);
        ValueTask<bool> TryAcquireAsync(MessageType type, Guid messageId);
        ValueTask Unlink(Guid id);
    }
}
