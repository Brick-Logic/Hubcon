using Hubcon.Shared.Abstractions.Enums;
using Microsoft.AspNetCore.Http;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type middlewareType)
        {
            MiddlewareType = middlewareType;
        }

        public UseMiddlewareAttribute(Type middlewareType, MiddlewareLifeCycle cycle)
        {
            MiddlewareType = middlewareType;
            Cycle = cycle;
        }

        public MiddlewareLifeCycle Cycle { get; } = MiddlewareLifeCycle.Scoped;
        public Type MiddlewareType { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseMiddlewareAttribute<T> : Attribute where T : class, IMiddleware
    {
        public UseMiddlewareAttribute()
        {
            
        }

        public UseMiddlewareAttribute(MiddlewareLifeCycle cycle)
        {
            Cycle = cycle;
        }

        public MiddlewareLifeCycle Cycle { get; } = MiddlewareLifeCycle.Scoped;
        public Type MiddlewareType { get; } = typeof(T);
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseHttpEndpointFilterAttribute(Type endpointFilterType) : Attribute
    {
        public Type EndpointFilterType { get; } = typeof(IEndpointFilter).IsAssignableFrom(endpointFilterType)
            ? endpointFilterType
            : throw new ArgumentException("The type used in the UseEndpointFilter attribute is not an IEndpointFilter type.");
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseContractMiddlewaresFirst : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseOperationMiddlewaresFirst : Attribute
    {
    }
}