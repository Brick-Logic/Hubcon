using Hubcon.Server.Abstractions.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public sealed class UseJwtAttribute : UseAuthAttribute<JwtAuthHandler>
    {
    }
}