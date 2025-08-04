using Hubcon.Shared.Abstractions.Standard.Cache;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Standard.Interceptor
{
    public abstract partial class BaseContractProxy
    {
        private static ImmutableCache<(Type, string), MethodInfo> Methods { get; } = new ImmutableCache<(Type, string), MethodInfo>();
        private Type _contractType;

        private IClientProxyInterceptor Interceptor { get; set; }

        public void BuildContractProxy(IClientProxyInterceptor interceptor)
        {
            Interceptor = interceptor;

            _contractType = GetType()
                .GetInterfaces()
                .First(x => typeof(IControllerContract).IsAssignableFrom(x) && x != typeof(IControllerContract));
       
            var methods = _contractType
                .GetMethods()
                .Where(x => !x.Name.Contains("get_") && !x.Name.Contains("set_"));

            foreach (var method in methods)
            {
                Methods.GetOrAdd((_contractType, method.GetMethodSignature()), x => method);
            }          
        }

        private MethodInfo GetMethod(string methodSignature)
        {
            Methods.TryGetValue((_contractType, methodSignature), out var method);
            return method;
        }

        public async Task<T> InvokeAsync<T>(string method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            return await Interceptor.InvokeAsync<T>(GetMethod(method), arguments , cancellationToken);
        }

        public Task CallAsync(string method, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            return Interceptor.CallAsync(GetMethod(method), arguments, cancellationToken);
        }
    }
}