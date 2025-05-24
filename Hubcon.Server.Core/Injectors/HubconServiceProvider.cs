using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Core.Injectors
{
    public class HubconServiceProvider : IHubconServiceProvider
    {
        private readonly IServiceProvider _innerService;

        public HubconServiceProvider(IServiceProvider innerService)
        {
            _innerService = innerService;
        }

        public object? GetService(Type serviceType)
        {
            var instance = _innerService.GetServiceWithInjector(serviceType);
            
            return this;
        }

        public object GetService<TInstanceType>(Type type, Action<IDependencyInjector<TInstanceType, object?>>? options = null) => GetService(type, options);

        public T GetServiceWithInjector<T>(Action<IDependencyInjector<T, object?>>? options = null)
        {
            return (T)GetServiceWithInjector(typeof(T))!;
        }

        public object? GetServiceWithInjector(Type type, Action<IDependencyInjector<object, object?>>? options = null)
        {
            var instance = _innerService.GetRequiredService(type);

            if (options != null)
            {
                _innerService.GetServiceWithInjector(instance, options);
            }
            else
            {
                _innerService.GetServiceWithInjector(instance);
            }

            return instance;
        }
    }

}
