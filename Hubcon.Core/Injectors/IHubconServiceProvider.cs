using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Injectors
{
    public interface IHubconServiceProvider : IServiceProvider
    {
        public object? GetService<TInstanceType>(Type type, Action<DependencyInjector<TInstanceType, object?>>? options = null);
        public T? GetServiceWithInjector<T>(Action<DependencyInjector<T, object?>>? options = null);
        public object? GetServiceWithInjector(Type type, Action<DependencyInjector<object, object?>>? options = null);

    }
}
