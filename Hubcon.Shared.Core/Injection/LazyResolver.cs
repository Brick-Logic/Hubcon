using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;

namespace Hubcon.Shared.Core.Injection
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class LazyResolver<T> : Lazy<T>
    {
        public LazyResolver(IServiceProvider provider) : base(() => provider.GetRequiredService<T>()) { }
    }
}
