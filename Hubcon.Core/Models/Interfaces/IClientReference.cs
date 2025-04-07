namespace Hubcon.Core.Models.Interfaces
{
    public interface IClientReference
    {
        public string Id { get; }
        public object? ClientInfo { get; set; }
        public IClientReference<TICommunicationContract> WithController<TICommunicationContract>(TICommunicationContract clientController) where TICommunicationContract : ICommunicationContract;
    }

    public interface IClientReference<TICommunicationContract> : IClientReference where TICommunicationContract : ICommunicationContract
    {
        public TICommunicationContract ClientController { get; init; }
    }
}
