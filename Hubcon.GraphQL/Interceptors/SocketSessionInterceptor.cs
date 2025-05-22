using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using HotChocolate.Execution;

namespace Hubcon.GraphQL.Interceptors
{
    public class SocketSessionInterceptor : DefaultSocketSessionInterceptor
    {
        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override ValueTask OnCloseAsync(ISocketSession session, CancellationToken cancellationToken = default)
        {
            return base.OnCloseAsync(session, cancellationToken);
        }

        public override ValueTask OnCompleteAsync(ISocketSession session, string operationSessionId, CancellationToken cancellationToken = default)
        {
            return base.OnCompleteAsync(session, operationSessionId, cancellationToken);
        }

        public async override ValueTask<ConnectionStatus> OnConnectAsync(ISocketSession session, IOperationMessagePayload connectionInitMessage, CancellationToken cancellationToken = default)
        {
            var user = session.Connection.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                await session.Connection.CloseAsync("Token expired", ConnectionCloseReason.PolicyViolation, cancellationToken);                
                return await base.OnConnectAsync(session, connectionInitMessage, cancellationToken);
            }

            var expClaim = user.FindFirst("exp")?.Value;

            if (long.TryParse(expClaim, out var expUnix))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var now = DateTimeOffset.UtcNow;

                var remaining = expDate - now;

                if (remaining <= TimeSpan.Zero)
                {
                    await session.Connection.CloseAsync("Token expired", ConnectionCloseReason.PolicyViolation, cancellationToken);
                    return await base.OnConnectAsync(session, connectionInitMessage, cancellationToken);
                }

                // Forzamos desconexión al vencer el token
                _ = Task.Delay(remaining, cancellationToken).ContinueWith(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        session.Connection.CloseAsync("Token expired", ConnectionCloseReason.PolicyViolation).GetAwaiter().GetResult();
                    }
                }, cancellationToken);
            }

            return await base.OnConnectAsync(session, connectionInitMessage, cancellationToken);
        }

        public override ValueTask<IReadOnlyDictionary<string, object?>?> OnPingAsync(ISocketSession session, IOperationMessagePayload pingMessage, CancellationToken cancellationToken = default)
        {
            return base.OnPingAsync(session, pingMessage, cancellationToken);
        }

        public override ValueTask OnPongAsync(ISocketSession session, IOperationMessagePayload pongMessage, CancellationToken cancellationToken = default)
        {
            return base.OnPongAsync(session, pongMessage, cancellationToken);
        }

        public override ValueTask OnRequestAsync(ISocketSession session, string operationSessionId, OperationRequestBuilder requestBuilder, CancellationToken cancellationToken = default)
        {
            return base.OnRequestAsync(session, operationSessionId, requestBuilder, cancellationToken);
        }

        public override ValueTask<IOperationResult> OnResultAsync(ISocketSession session, string operationSessionId, IOperationResult result, CancellationToken cancellationToken = default)
        {
            return base.OnResultAsync(session, operationSessionId, result, cancellationToken);
        }

        public override string? ToString()
        {
            return base.ToString();
        }
    }

}
