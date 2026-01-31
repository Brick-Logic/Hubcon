using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface ITransportClient
    {
        public Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
    }
}

namespace Hubcon
{
    public interface ITransportClient<TAttribute> : ITransportClient
        where TAttribute : HubconTransportAttribute
    {
    }
}
