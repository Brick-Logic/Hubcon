using Castle.DynamicProxy;

namespace Hubcon.Shared.Abstractions.Standard.Interfaces
{
    public interface IClientProxy
    {
        void UseInterceptor(AsyncInterceptorBase interceptor);
    }
}
