using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Delegates
{
#pragma warning disable CS1591

    public delegate ValueTask<HubconResponse> ResultHandlerDelegate(object? result);
#pragma warning restore CS1591
}