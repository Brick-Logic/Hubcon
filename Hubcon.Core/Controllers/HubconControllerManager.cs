using Hubcon.Core.Handlers;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager : IHubconControllerManager
    {
        // Handlers
        public IRequestPipeline Pipeline { get; set; }
        public ICommunicationHandler CommunicationHandler { get; set; }

        private HubconControllerManager(ICommunicationHandler communicationHandler)
        {
            Pipeline = new RequestPipeline();
            CommunicationHandler = communicationHandler;
        }

        public static IHubconControllerManager GetControllerManager(ICommunicationHandler communicationHandler)
        {
            return new HubconControllerManager(communicationHandler);
        }
    }
}
