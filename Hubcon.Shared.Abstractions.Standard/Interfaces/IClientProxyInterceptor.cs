using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Standard.Interfaces
{
    public interface IClientProxyInterceptor
    {
        Task<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object> arguments, JsonSerializerContext context, CancellationToken cancellationToken);
        Task CallAsync(MethodInfo method, Dictionary<string, object> arguments, JsonSerializerContext context, CancellationToken cancellationToken);
        Task<T> IngestAsync<T>(MethodInfo method, Dictionary<string, object> arguments, JsonSerializerContext context, CancellationToken cancellationToken);
        Task IngestAsync(MethodInfo method, Dictionary<string, object> arguments, JsonSerializerContext context, CancellationToken cancellationToken);
        IAsyncEnumerable<T> StreamAsync<T>(MethodInfo method, Dictionary<string, object> arguments, JsonSerializerContext context, CancellationToken cancellationToken);
    }
}
