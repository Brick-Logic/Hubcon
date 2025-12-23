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
        public bool WebsocketMethodsEnabled { get; }
        public Type ContractType { get; }
        ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; }
        IReadOnlyDictionary<HookType, Func<InvocationContext, Task>> Hooks { get; }
        bool RemoteCancellationIsAllowed { get; }
        bool HttpAuthIsEnabled { get; }
        Task CallHook(HookType hookType, InvocationContext context);
        IOperationOptions GetOperationOptions(string operationName, MemberInfo memberInfo);
        bool IsWebsocketOperation(string operationName);
    }
}
