using Hubcon.Shared.Core.Websockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public interface IGlobalRateLimiterManager
    {
        ValueTask Link(string anchorKey, Guid id, HubconTransportAttribute transportAttribute, IOperationRequest request);
        ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, HubconTransportAttribute transport, IOperationRequest? operation = null, CancellationToken cancellationToken = default);
        ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, Guid resourceId, CancellationToken cancellationToken = default);
        ValueTask Unlink(string anchorKey, Guid operationId);
    }
}
