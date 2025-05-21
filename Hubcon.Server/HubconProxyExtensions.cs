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

namespace Hubcon.Server
{
    public static class HubconProxyExtensions
    {
        public static WebApplicationBuilder UseContractsFromAssembly(this WebApplicationBuilder e, string assemblyName)
        {
            return HubconServerBuilder.Current.UseContractsFromAssembly(e, assemblyName);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, string assemblyName)
        {
            return HubconServerBuilder.Current.AddContractsFromAssembly(serviceProvider, assemblyName);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, Assembly assembly)
        {
            return HubconServerBuilder.Current.AddContractsFromAssembly(serviceProvider, assembly);
        }
    }
}
