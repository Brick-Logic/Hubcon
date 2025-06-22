using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Standard.Interceptor
{
    public abstract class BaseContractProxy
    {
        private static ConcurrentDictionary<(Type, string), MethodInfo> Methods { get; } = new ConcurrentDictionary<(Type, string), MethodInfo>();

        private readonly Type _contractType;

        protected BaseContractProxy(IClientProxyInterceptor interceptor)
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
                Methods.TryAdd((_contractType, method.GetMethodSignature()), method);
            }          
        }

        private IClientProxyInterceptor Interceptor { get; }

        private MethodInfo GetMethod(string methodSignature)
        {
            Methods.TryGetValue((_contractType, methodSignature), out var method);
            return method;
        }

        public Task<T> InvokeAsync<T>(string method, params object[] arguments)
        {
            return Interceptor.InvokeAsync<T>(GetMethod(method), arguments);
        }

        public Task CallAsync(string method, params object[] arguments)
        {
            return Interceptor.CallAsync(GetMethod(method), arguments);
        }
    }
}