using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hubcon
{
    public static class FactoryMetadata
    {
        private static IImmutableDictionary<Type, Func<IServiceProvider, object>>? _factories;

        public static void Setup(IImmutableDictionary<Type, Func<IServiceProvider, object>> factories)
        {
            _factories ??= factories;
        }

        public static Func<IServiceProvider, object>? TryGetFactory<T>() => TryGetFactory(typeof(T));

        public static Func<IServiceProvider, object>? TryGetFactory(Type serviceType)
        {
            if (_factories.TryGetValue(serviceType, out var value))
            {
                return value;
            }

            return null;
        }

        public static IServiceCollection RegisterFactorySingleton<TService>(this IServiceCollection serviceCollection)
            where TService : class => serviceCollection.RegisterFactorySingleton(typeof(TService));
        
        public static IServiceCollection RegisterFactorySingleton(this IServiceCollection serviceCollection, Type serviceType)
        {
            var factory = TryGetFactory(serviceType);
            
            if (factory == null)
                throw new ArgumentNullException(
                    $"A hubcon-generated factory for type {serviceType.Name} could not be found. Did you miss the [HubconPreserve] or [HubconConstructor] attributes?");

            serviceCollection.TryAddSingleton(serviceType, factory);

            return serviceCollection;
        }
        

        public static IServiceCollection RegisterFactoryScoped<TService>(this IServiceCollection serviceCollection)
            where TService : class => serviceCollection.RegisterFactoryScoped(typeof(TService));
        
        public static IServiceCollection RegisterFactoryScoped(this IServiceCollection serviceCollection, Type serviceType)
        {
            var factory = TryGetFactory(serviceType);
            
            if (factory == null)
                throw new ArgumentNullException(
                    $"A hubcon-generated factory for type {serviceType.Name} could not be found. Did you miss the [HubconPreserve] or [HubconConstructor] attributes?");

            serviceCollection.TryAddScoped(serviceType, factory);

            return serviceCollection;
        }

        public static IServiceCollection RegisterFactoryTransient<TService>(this IServiceCollection serviceCollection)
            where TService : class => serviceCollection.RegisterFactoryTransient(typeof(TService));
        
        public static IServiceCollection RegisterFactoryTransient(this IServiceCollection serviceCollection, Type serviceType)
        {
            var factory = TryGetFactory(serviceType);
            
            if (factory == null)
                throw new ArgumentNullException(
                    $"A hubcon-generated factory for type {serviceType.Name} could not be found. Did you miss the [HubconPreserve] or [HubconConstructor] attributes?");

            serviceCollection.TryAddTransient(serviceType, factory);

            return serviceCollection;
        }
    }
}