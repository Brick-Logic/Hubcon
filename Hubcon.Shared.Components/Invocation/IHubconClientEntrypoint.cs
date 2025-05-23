using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Components.Invocation
{
    public interface IHubconClientEntrypoint : IBaseHubconController
    {
        public void Build(WebApplication? app = null);
    }
}
