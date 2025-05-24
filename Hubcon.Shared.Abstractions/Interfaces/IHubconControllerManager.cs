namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconControllerManager
    {
        public IRequestHandler Pipeline { get; }
        public ICommunicationHandler CommunicationHandler { get; }
    }

    public interface IHubconControllerManager<TICommunicationHandler> : IHubconControllerManager
    where TICommunicationHandler : ICommunicationHandler
    {
        
    }
}
