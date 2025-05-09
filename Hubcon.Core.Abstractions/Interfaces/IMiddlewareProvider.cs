using Hubcon.Core.Abstractions.Delegates;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMiddlewareProvider
    {
        public IPipeline GetPipeline(IMethodDescriptor descriptor, IMethodInvokeRequest request, InvocationDelegate handler);
    }
}
