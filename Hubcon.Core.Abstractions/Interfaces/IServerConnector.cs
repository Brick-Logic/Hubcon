using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IServerConnector
    {
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
