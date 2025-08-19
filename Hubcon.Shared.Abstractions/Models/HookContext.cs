using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Models
{
    public sealed record class HookContext(
        HookType Type,
        IServiceProvider Services,
        IOperationRequest Request,
        CancellationToken CancellationToken,
        object? Result = null,
        Exception? Exception = null);
}