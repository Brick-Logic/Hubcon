using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Standard.Interfaces
{
    public interface IClientProxyInterceptor
    {
        Task<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
        Task CallAsync(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
        Task<T> IngestAsync<T>(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
        Task IngestAsync(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
        IAsyncEnumerable<T> StreamAsync<T>(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
    }
}
