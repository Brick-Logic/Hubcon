using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddHubcon(this IServiceCollection services) 
        {
            services.AddSignalR().AddMessagePackProtocol();

            return services;
        }
    }
}
