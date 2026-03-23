#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPipeline
    {
        public Task<IHubconResponse> Execute();
    }
}
