using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IHubconControllerManager
    {
        public IRequestHandler Pipeline { get; }
        public ICommunicationHandler CommunicationHandler { get; }
    }

    public interface IHubconControllerManager<TICommunicationHandler> : IHubconControllerManager
    where TICommunicationHandler : ICommunicationHandler
    {
        // Handlers
    }

    public interface IHubconClientControllerManager<TICommunicationHandler> : IHubconControllerManager<TICommunicationHandler>
         where TICommunicationHandler : ICommunicationHandler
    {
        public new TICommunicationHandler CommunicationHandler { get; }
    }

    public interface IHubconServerControllerManager<TICommunicationHandler> : IHubconControllerManager<TICommunicationHandler>
         where TICommunicationHandler : IServerCommunicationHandler
    {
        public new TICommunicationHandler CommunicationHandler { get; }
    }
}
