using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationConfigurator
    {
        IOperationConfigurator AddHook(HookType onSend, Func<HookContext, Task> hookDelegate);
        IOperationConfigurator AddValidationHook(Func<RequestValidationContext, Task> value);
        IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true);
        IOperationConfigurator UseTransport(TransportType transportType);
    }
}