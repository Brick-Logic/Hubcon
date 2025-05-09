using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IClientReference
    {
        public string Id { get; }
        public object? ClientInfo { get; set; }
        public IClientReference<TICommunicationContract> WithController<TICommunicationContract>(TICommunicationContract clientController) where TICommunicationContract : IControllerContract;
    }

    public interface IClientReference<TICommunicationContract> : IClientReference where TICommunicationContract : IControllerContract
    {
        public TICommunicationContract ClientController { get; init; }
    }
}
