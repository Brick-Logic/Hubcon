using Hubcon.Core.Models.Middleware;

namespace Hubcon.Core.Models.Pipeline.Interfaces
{
    public interface IPipelineOptions
    {
        public IPipelineOptions AddMiddleware<T>() where T : class, IMiddleware;
    }
}
