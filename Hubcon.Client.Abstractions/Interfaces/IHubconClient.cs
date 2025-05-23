using Autofac;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, CancellationToken cancellationToken = default);
        void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IComponentContext context, bool useSecureConnection = true);
        Task Start();
    }
}
