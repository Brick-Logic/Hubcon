using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconServerConnector<T>
    {
        ICommunicationHandler Connection { get; }

        TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
