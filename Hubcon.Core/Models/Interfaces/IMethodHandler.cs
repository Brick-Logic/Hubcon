using Hubcon.Core.Handlers;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IRequestPipeline
    {
        public void RegisterMethods(Type type, Action<HubconMethodInvoker>? forEachMethodAction = null);
        public Task HandleWithoutResultAsync(object instance, MethodInvokeRequest methodInfo);
        public Task<IMethodResponse> HandleSynchronousResult(object instance, MethodInvokeRequest methodInfo);
        public Task HandleSynchronous(object instance, MethodInvokeRequest methodInfo);
        public IAsyncEnumerable<JsonElement?> GetStream(object instance, MethodInvokeRequest methodInfo);
        public Task<IMethodResponse> HandleWithResultAsync(object instance, MethodInvokeRequest methodInfo);
    }
}
