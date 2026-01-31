using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Delegates
{
    public delegate Task<HubconResponse> ResultHandlerDelegate(object? result);
}