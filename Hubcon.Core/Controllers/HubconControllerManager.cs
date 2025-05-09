using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager : IHubconControllerManager
    {
        public IControllerInvocationHandler Pipeline { get; set; }
        public ICommunicationHandler CommunicationHandler { get; }

        public HubconControllerManager(ICommunicationHandler communicationHandler, IControllerInvocationHandler requestPipeline)
        {
            Pipeline = requestPipeline;
            CommunicationHandler = communicationHandler;
        }
    }
}
