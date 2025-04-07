using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Middleware
{
    internal class MiddlewareOptions : IMiddlewareOptions
    {
        private IMiddlewareProvider middlewareProvider;

        public MiddlewareOptions(IMiddlewareProvider middlewareProvider)
        {
            this.middlewareProvider = middlewareProvider;
        }

        public void AddMiddleware<TMiddleware, TController>()
            where TMiddleware : IHubconMiddleware
            where TController : IBaseHubconController
        {
            middlewareProvider.AddMiddleware<TMiddleware, TController>();
        }
    }

    internal class MiddlewareProvider : IMiddlewareProvider
    {
        private Dictionary<Type, List<Type>> middlewares = new();

        public MiddlewareProvider()
        {
        }

        public void AddMiddleware<TMiddleware, TController>()
            where TMiddleware : IHubconMiddleware
            where TController : IBaseHubconController
        {
            if (middlewares.TryGetValue(typeof(TController), out List<Type>? value))
                value.Add(typeof(TMiddleware));
            else
                middlewares.Add(typeof(TController), new() { typeof(TMiddleware) });
        }

        public IEnumerable<Type> GetMiddlewares<TController>() 
            where TController : IBaseHubconController
        {
            if (middlewares.TryGetValue(typeof(TController), out List<Type>? value))
                return value;
            else
                return Enumerable.Empty<Type>();

        }
    }
}
