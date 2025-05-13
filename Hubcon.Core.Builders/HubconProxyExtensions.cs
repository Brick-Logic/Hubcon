using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Builders
{

    public static class HubconProxyExtensions
    {
        public static WebApplicationBuilder UseContractsFromAssembly(this WebApplicationBuilder e, string assemblyName)
        {
            return HubconBuilder.Current.UseContractsFromAssembly(e, assemblyName);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, string assemblyName)
        {
            return HubconBuilder.Current.AddContractsFromAssembly(serviceProvider, assemblyName);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, Assembly assembly)
        {
            return HubconBuilder.Current.AddContractsFromAssembly(serviceProvider, assembly);
        }
    }
}
