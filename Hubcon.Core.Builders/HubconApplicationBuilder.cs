using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;

namespace Hubcon.Core.Builders
{
    public class HubconApplicationBuilder : IHubconApplicationBuilder
    {
        public IServiceCollection Services { get; }

        internal HubconApplicationBuilder()
        {
            Services = new ServiceCollection();
        }

        public IHubconClientApplication Build(Action<IServiceCollection>? services)
        {
            services?.Invoke(Services);
            return new HubconApplication(Services.BuildServiceProvider());
        }
    }
}
