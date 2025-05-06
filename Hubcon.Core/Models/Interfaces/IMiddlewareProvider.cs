using Hubcon.Core.MethodHandling;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IMiddlewareProvider
    {
        public IPipeline GetPipeline(MethodDescriptor descriptor, MethodInvokeRequest request, InvocationDelegate handler);
    }
}
