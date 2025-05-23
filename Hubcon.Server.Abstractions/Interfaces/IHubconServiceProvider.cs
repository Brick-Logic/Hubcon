using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IHubconServiceProvider : IServiceProvider
    {
        public object? GetService<TInstanceType>(Type type, Action<IDependencyInjector<TInstanceType, object?>>? options = null);
        public T? GetServiceWithInjector<T>(Action<IDependencyInjector<T, object?>>? options = null);
        public object? GetServiceWithInjector(Type type, Action<IDependencyInjector<object, object?>>? options = null);

    }
}
