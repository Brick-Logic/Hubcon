using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IHubconServiceProvider : IServiceProvider
    {
        public object? GetService<TInstanceType>(Type type, Action<IDependencyInjector<TInstanceType, object?>>? options = null);
        public T? GetServiceWithInjector<T>(Action<IDependencyInjector<T, object?>>? options = null);
        public object? GetServiceWithInjector(Type type, Action<IDependencyInjector<object, object?>>? options = null);

    }
}
