using Hubcon.Shared.Abstractions.Standard.Interfaces;
#pragma warning disable CS1591
namespace Hubcon.Experimental
{
    public interface IServerConnector
    {
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
#pragma warning restore CS1591
