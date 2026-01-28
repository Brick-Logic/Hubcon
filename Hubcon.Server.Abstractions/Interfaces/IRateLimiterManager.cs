using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IRateLimiterManager
    {
        ValueTask DisposeAsync();
        ValueTask Link(Guid id, TransportAttribute transportAttribute, IOperationRequest request);
        ValueTask<bool> TryAcquireAsync(MessageType type, TransportAttribute transportAttribute, IOperationEndpoint? operation = null);
        ValueTask<bool> TryAcquireAsync(MessageType type, Guid messageId);
        ValueTask Unlink(Guid id);
    }
}
