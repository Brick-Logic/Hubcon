using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Hubcon;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    /// <summary>
    /// Base attribute class for hubcon auth attributes
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    [HubconPreserve]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public abstract class UseAuthAttribute<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler> 
        : Attribute, IUseAuthAttribute, IRegisterer<THandler>
        where THandler : class, IAuthHandler
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public UseAuthAttribute()
        {
        }

        /// <summary>
        /// Constructor with defined lifecycle.
        /// </summary>
        /// <param name="cycle"></param>
        public UseAuthAttribute(LifeCycle cycle)
        {
            Cycle = cycle;
        }

        /// <summary>
        /// The defined lifecycle for the provided middleware.
        /// </summary>
        public LifeCycle Cycle { get; } = LifeCycle.Scoped;
        
        /// <summary>
        /// The auth handler type
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type HandlerType => typeof(THandler);
        IServiceCollection IRegisterer.Register(IServiceCollection serviceCollection)
        {
            switch (Cycle)
            {
                case LifeCycle.Scoped:
                    serviceCollection.RegisterFactoryScoped<THandler>();
                    break;
                case LifeCycle.Transient:
                    serviceCollection.RegisterFactoryTransient<THandler>();
                    break;
                case LifeCycle.Singleton:
                    serviceCollection.RegisterFactorySingleton<THandler>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Cycle), Cycle, null);
            }

            return serviceCollection;
        }
        
        THandler IRegisterer<THandler>.Get(IServiceProvider services)
        {
            return services.GetRequiredService<THandler>();
        }
        
        TService IRegisterer.Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(IServiceProvider services) where TService : class
        {
            return (services.GetRequiredService<THandler>() as TService)!;
        }
    }
}
