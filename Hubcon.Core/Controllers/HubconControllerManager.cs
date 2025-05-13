using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager : IHubconControllerManager
    {
        public IRequestHandler Pipeline { get; set; }
        public ICommunicationHandler CommunicationHandler { get; }

        public HubconControllerManager(ICommunicationHandler communicationHandler, IRequestHandler requestPipeline)
        {
            Pipeline = requestPipeline;
            CommunicationHandler = communicationHandler;
        }
    }
}
