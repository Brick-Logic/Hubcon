#pragma warning disable CS1591
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Hubcon.Shared.Core.Injection
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class LazyResolver<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : Lazy<T>
    {
        public LazyResolver(IServiceProvider provider) : base(() => provider.GetRequiredService<T>()) { }
    }
}
