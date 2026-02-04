using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
        ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
        ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        ValueTask<IObservable<JsonElement>> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
    }
}