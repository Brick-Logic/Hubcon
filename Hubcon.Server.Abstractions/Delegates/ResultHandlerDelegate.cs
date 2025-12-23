using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Delegates
{
    public delegate Task<IOperationResult> ResultHandlerDelegate(object? result);
}