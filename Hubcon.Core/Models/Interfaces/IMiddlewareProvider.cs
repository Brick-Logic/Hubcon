using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IMiddlewareProvider
    {
        //public void AddMiddlewares<TController>(Action<IMiddlewareOptions> options) where TController : IBaseHubconController;

        public IPipeline GetPipeline(Type controllerType, MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler);
    }
}
