using System.Diagnostics.CodeAnalysis;
using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hubcon
{
    /// <summary>
    /// Specifies an IMiddleware to be executed during the operation lifecycle.
    /// Can be applied to classes, properties, or methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public abstract class UseMiddlewareAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public UseMiddlewareAttribute()
        {
        }
        
        /// <summary>
        /// Constructor with the middleware type.
        /// </summary>
        /// <param name="middlewareType"></param>
        public UseMiddlewareAttribute(Type middlewareType)
        {
            MiddlewareType = middlewareType;
        }

        /// <summary>
        /// Constructor with the middleware type and lifecycle.
        /// </summary>
        /// <param name="middlewareType"></param>
        /// <param name="cycle"></param>
        public UseMiddlewareAttribute(Type middlewareType, LifeCycle cycle)
        {
            MiddlewareType = middlewareType;
            Cycle = cycle;
        }

        /// <summary>Defines the DI lifecycle of the middleware (Scoped, Singleton, or Transient).</summary>
        public LifeCycle Cycle { get; } = LifeCycle.Scoped;

        /// <summary>The type of the middleware to instantiate.</summary>
        public Type MiddlewareType { get; }
    }

    /// <summary>
    /// Generic version of UseMiddlewareAttribute for strongly-typed middleware registration.
    /// Ensures at compile-time that the type implements IMiddleware.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseMiddlewareAttribute<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
        : UseMiddlewareAttribute, IRegisterer<TMiddleware>
        where TMiddleware : class, IMiddleware
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public UseMiddlewareAttribute() : base(typeof(TMiddleware))
        {
        }

        /// <summary>
        /// Constructor with defined lifecycle.
        /// </summary>
        /// <param name="cycle"></param>
        public UseMiddlewareAttribute(LifeCycle cycle) : base(typeof(TMiddleware), cycle)
        {
            
        }
        
        IServiceCollection IRegisterer.Register(IServiceCollection serviceCollection)
        {
            switch (Cycle)
            {
                case LifeCycle.Scoped:
                    serviceCollection.RegisterFactoryScoped<TMiddleware>();
                    break;
                case LifeCycle.Transient:
                    serviceCollection.RegisterFactoryTransient<TMiddleware>();
                    break;
                case LifeCycle.Singleton:
                    serviceCollection.RegisterFactorySingleton<TMiddleware>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Cycle), Cycle, null);
            }

            return serviceCollection;
        }
        
        TMiddleware IRegisterer<TMiddleware>.Get(IServiceProvider services)
        {
            return services.GetRequiredService<TMiddleware>();
        }
        
        TGet IRegisterer.Get<TGet>(IServiceProvider services) where TGet: class
        {
            return (services.GetRequiredService<TMiddleware>() as TGet)!;
        }
    }

    /// <summary>
    /// Integrates standard ASP.NET Core IEndpointFilter logic into the Hubcon HTTP transport.
    /// This bridge allows you to reuse existing Web API filters within your Hubcon services.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseHttpEndpointFilterAttribute(Type endpointFilterType) : Attribute
    {
        /// <summary>
        /// Gets the type of the endpoint filter. Validates that it implements IEndpointFilter.
        /// </summary>
        public Type EndpointFilterType { get; } = typeof(IEndpointFilter).IsAssignableFrom(endpointFilterType)
            ? endpointFilterType
            : throw new ArgumentException("The type must implement IEndpointFilter.");
    }

    /// <summary>
    /// Forces the middleware defined at the contract (interface) level to be executed 
    /// before any other middleware in the pipeline for the decorated operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseContractMiddlewaresFirst : Attribute
    {
    }

    /// <summary>
    /// Forces the middleware defined at the operation (method) level to be executed 
    /// before any contract-level or global middleware.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseOperationMiddlewaresFirst : Attribute
    {
    }
}