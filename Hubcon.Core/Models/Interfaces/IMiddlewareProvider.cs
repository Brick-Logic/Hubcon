using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    internal interface IMiddlewareProvider
    {
        public void AddMiddleware<TMiddleware, TController>() where TMiddleware : IHubconMiddleware where TController : IBaseHubconController;
        public IEnumerable<Type> GetMiddlewares<TController>() where TController : IBaseHubconController;
    }

    internal interface IMiddlewareOptions
    {
        public void AddMiddleware<TMiddleware, TController>()
            where TMiddleware : IHubconMiddleware
            where TController : IBaseHubconController;
    }
}
