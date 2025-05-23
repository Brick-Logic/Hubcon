using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IServerConnector
    {
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
