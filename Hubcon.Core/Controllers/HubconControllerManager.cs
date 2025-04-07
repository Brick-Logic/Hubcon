using Hubcon.Core.Handlers;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager : IHubconControllerManager
    {
        // Handlers
        public IMethodHandler Methods { get; set; }
        public ICommunicationHandler CommunicationHandler { get; set; }

        private HubconControllerManager(ICommunicationHandler communicationHandler)
        {
            Methods = new MethodHandler();
            CommunicationHandler = communicationHandler;
        }

        public static IHubconControllerManager GetControllerManager(ICommunicationHandler communicationHandler)
        {
            return new HubconControllerManager(communicationHandler);
        }
    }
}
