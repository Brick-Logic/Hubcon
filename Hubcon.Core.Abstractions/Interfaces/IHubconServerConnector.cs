using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IHubconServerConnector<T>
    {
        ICommunicationHandler Connection { get; }

        TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
