using Hubcon.Shared.Abstractions.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationConfigurator
    {
        public IOperationConfigurator UseTransport(TransportType transportType);
    }
}
