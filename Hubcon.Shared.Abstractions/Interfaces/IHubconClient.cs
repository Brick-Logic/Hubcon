using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default);
        Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
        void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IServiceProvider services, IDictionary<Type, IContractOptions> contractOptions, bool useSecureConnection = true);
        Task Ingest(IOperationRequest request, Dictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default);
    }
}