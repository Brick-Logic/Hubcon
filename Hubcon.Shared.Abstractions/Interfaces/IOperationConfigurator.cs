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
        IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true);
        IOperationConfigurator UseTransport(TransportType transportType);
    }
}
