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
        ValueTask<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken);
        Task CallAsync(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken);
    }
}
