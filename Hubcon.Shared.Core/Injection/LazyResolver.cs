using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Injection
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class LazyResolver<T> : Lazy<T>
    {
        public LazyResolver(IServiceProvider provider) : base(() => provider.GetRequiredService<T>()) { }
    }
}
