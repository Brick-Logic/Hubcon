using Hubcon.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Core.Handlers
{
    public interface IMethodHandler
    {
        public void BuildMethods(object instance, Type type, Action<string, MethodInfo, MethodHandler>? forEachMethodAction = null);
        public Delegate CreateAction(MethodInfo method, object instance);
        public Task HandleWithoutResultAsync(MethodInvokeRequest methodInfo);
        public Task<MethodResponse> HandleSynchronousResult(MethodInvokeRequest methodInfo);
        public Task HandleSynchronous(MethodInvokeRequest methodInfo);
        public IAsyncEnumerable<object> GetStream(MethodInvokeRequest methodInfo);
        public Task<MethodResponse> HandleWithResultAsync(MethodInvokeRequest methodInfo);
    }
}
