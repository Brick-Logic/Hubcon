using Hubcon.Core.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Hubcon.Core.Injectors
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

        public object GetService<TInstanceType>(Type type, Action<DependencyInjector<TInstanceType, object?>>? options = null) => GetService(type, options);

        public T GetServiceWithInjector<T>(Action<DependencyInjector<T, object?>>? options = null)
        {
            return (T)GetServiceWithInjector(typeof(T))!;
        }

        public object? GetServiceWithInjector(Type type, Action<DependencyInjector<object, object?>>? options = null)
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
