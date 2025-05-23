using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPipeline
    {
        public Task<IObjectOperationResponse> Execute();
    }
}
