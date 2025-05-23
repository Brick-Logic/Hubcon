using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IContractInterceptor
    {
        ICommunicationHandler CommunicationHandler { get; }
    }
}
