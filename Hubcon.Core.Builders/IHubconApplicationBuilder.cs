using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core.Builders
{
    public interface IHubconApplicationBuilder
    {
        IServiceCollection Services { get; }

        IHubconClientApplication Build(Action<IServiceCollection> services);
    }
}
