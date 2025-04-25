using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IMiddlewareProvider
    {
        public IPipeline GetPipeline(HubconMethodInvoker methodInvoker, MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler);
    }
}
