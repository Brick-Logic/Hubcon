using Hubcon.Shared.Abstractions.Standard.Interfaces;
#pragma warning disable CS1591
namespace Hubcon.Server.Abstractions.Interfaces
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
#pragma warning restore CS1591