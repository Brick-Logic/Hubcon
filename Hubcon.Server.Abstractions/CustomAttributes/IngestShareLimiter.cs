using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Indicates that ingests should share the same rate limiter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class IngestShareLimiter : Attribute
    {
    }
}
