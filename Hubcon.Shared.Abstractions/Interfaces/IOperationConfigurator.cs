using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationConfigurator : Hubcon.Shared.Abstractions.Standard.Interfaces.IOperationConfigurator
    {
        IOperationConfigurator AddHook(HookType onSend, Func<HookContext, Task> hookDelegate);
        IOperationConfigurator AddValidationHook(Func<RequestValidationContext, Task> value);
        IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true);
        IOperationConfigurator UseTransport(TransportType transportType);
        IOperationConfigurator AllowRemoteCancellation(bool value = true);
        IOperationConfigurator DisableHttpAuthentication();
    }
}