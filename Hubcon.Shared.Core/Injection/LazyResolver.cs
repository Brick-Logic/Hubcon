using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Injection
{
    public class LazyResolver<T> : Lazy<T>
    {
        public LazyResolver(IServiceProvider provider) : base(() => provider.GetRequiredService<T>()) { }
    }
}
