using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Middlewares
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseMiddlewareAttribute(Type middlewareType) : Attribute
    {
        public Type MiddlewareType { get; } = typeof(Abstractions.Interfaces.IMiddleware).IsAssignableFrom(middlewareType) 
            ? middlewareType 
            : throw new ArgumentException("The type used in the UseMiddleware attribute is not a Hubcon Middleware type.");
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