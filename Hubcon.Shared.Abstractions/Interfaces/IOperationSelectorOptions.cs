using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IGlobalOperationOptions
    {
        Dictionary<string, IOperationOptions> OperationOptions { get; }
    }
}
