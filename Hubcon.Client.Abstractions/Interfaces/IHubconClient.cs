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
        Task<T> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
        Task CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        Task<IObservable<JsonElement>> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        Task<T> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
    }
}