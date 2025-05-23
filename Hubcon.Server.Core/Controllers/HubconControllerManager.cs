using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Controllers
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
