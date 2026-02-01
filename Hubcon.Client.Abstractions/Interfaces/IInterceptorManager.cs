using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IInterceptorManager
    {
        IServiceProvider ServiceProvider { get; }
        IClientOptions ClientOptions { get; }
        IContractOptions ContractOptions { get; }
        IOperationOptions? OperationOptions { get; }
        IOperationRequest? Request { get; }

        Task CallHooks(HookType hookType, CancellationToken cancellationToken = default);
        Task CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default);
        Task CallInterceptor(InterceptorType interceptorType, CancellationToken cancellationToken = default);
        Task CallValidationHooks(CancellationToken cancellationToken = default);
    }
}
