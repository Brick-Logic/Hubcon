using Hubcon.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Middleware
{
    public delegate Task<IMethodResponse?> InvocationDelegate();
}
