using Hubcon.Core.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IHubconApplicationBuilder
    {
        IServiceCollection Services { get; }

        IHubconClientApplication Build(Action<IServiceCollection> services);
    }
}
