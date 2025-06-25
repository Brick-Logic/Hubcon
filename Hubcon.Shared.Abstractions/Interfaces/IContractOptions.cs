using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IContractOptions
    {
        public bool WebsocketMethodsEnabled { get; }
        public Type ContractType { get; }
    }
}
