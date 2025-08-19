using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, MethodInfo method, CancellationToken cancellationToken = default);
        Task<IAsyncEnumerable<JsonElement>> GetSubscription(IOperationRequest request, MemberInfo memberInfo, CancellationToken cancellationToken = default);
        void Build(IClientOptions builder, IServiceProvider services, IDictionary<Type, IContractOptions> contractOptions, bool useSecureConnection = true);
        Task<T> Ingest<T>(IOperationRequest request, MethodInfo method, CancellationToken cancellationToken);
    }
}