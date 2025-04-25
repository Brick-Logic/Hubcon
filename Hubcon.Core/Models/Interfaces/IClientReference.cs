namespace Hubcon.Core.Models.Interfaces
{
    public interface IClientReference
    {
        public string Id { get; }
        public object? ClientInfo { get; set; }
        public IClientReference<TICommunicationContract> WithController<TICommunicationContract>(TICommunicationContract clientController) where TICommunicationContract : IHubconControllerContract;
    }

    public interface IClientReference<TICommunicationContract> : IClientReference where TICommunicationContract : IHubconControllerContract
    {
        public TICommunicationContract ClientController { get; init; }
    }
}
