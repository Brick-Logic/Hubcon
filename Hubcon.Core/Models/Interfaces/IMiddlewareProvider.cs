using Hubcon.Core.Middleware;
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
        public void AddMiddlewares<TController>(Action<IPipelineOptions> options) where TController : IBaseHubconController;

        public IPipeline GetPipeline(Type controllerType, MethodInvokeRequest request, Func<Task<MethodResponse?>> handler);
    }
}
