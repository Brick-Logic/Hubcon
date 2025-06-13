using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<IOperationResponse<JsonElement>> SendAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
        void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IServiceProvider services, bool useSecureConnection = true);
    }
}