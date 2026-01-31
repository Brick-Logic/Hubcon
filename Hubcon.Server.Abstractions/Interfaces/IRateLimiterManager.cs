using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;

namespace Hubcon
{
    public interface IRateLimiterManager
    {
        ValueTask DisposeAsync();
        ValueTask Link(Guid id, HubconTransportAttribute transportAttribute, IOperationRequest request);
        ValueTask<bool> TryAcquireAsync(MessageType type, HubconTransportAttribute transportAttribute, IOperationRequest? operation = null);
        ValueTask<bool> TryAcquireAsync(MessageType type, Guid messageId);
        ValueTask Unlink(Guid id);
    }
}