using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationEndpoint
    {
        string ContractName { get; }
        string OperationName { get; }
    }
}
