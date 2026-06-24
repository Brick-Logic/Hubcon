using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IMessageRouter
    {
        void BeginRequest(Guid id);
        IIngestSession<T> CreateIngest<T>(Guid id, string connectionId, IOperationRequest request, IOperationOptions operationOptions, Action? onFinishedCallback = null);
        IStreamSession<T> CreateStream<T>(Guid id, string connectionId, IOperationRequest request, Action? onFinishedCallback = null);
        ValueTask DisposeAsync();
        void EndRequest(Guid id);
        ValueTask<BaseMessage?> GetResponseAsync(Guid id, TimeSpan timeout, CancellationToken cancellationToken);
        void Start();
    }
}