using Hubcon.Core.Handlers;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager : IHubconControllerManager
    {
        // Handlers
        public IControllerInvocationHandler Pipeline { get; set; }
        public ICommunicationHandler CommunicationHandler { get; }

        public HubconControllerManager(ICommunicationHandler communicationHandler, IControllerInvocationHandler requestPipeline)
        {
            Pipeline = requestPipeline;
            CommunicationHandler = communicationHandler;
        }
    }
}
