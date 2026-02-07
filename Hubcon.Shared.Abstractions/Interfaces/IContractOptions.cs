using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IContractOptions
    {
        public Type ContractType { get; }
        ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; }
        IReadOnlyDictionary<HookType, Func<IInvocationContext, Task>> Hooks { get; }
        bool RemoteCancellationIsAllowed { get; }

        Task CallHook(HookType hookType, IInvocationContext context);
        IOperationOptions GetOperationOptions(string operationName, MemberInfo memberInfo);
        HubconTransportAttribute? TransportType { get; }
        bool? AuthIsEnabled { get; }
        Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }
    }
}
