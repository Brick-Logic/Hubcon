using Hubcon.Core.Handlers;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Controllers
{
    public class HubconControllerManager<TICommunicationHandler> : IHubconControllerManager<TICommunicationHandler>
        where TICommunicationHandler : ICommunicationHandler
    {
        // Handlers
        public IRequestPipeline Pipeline { get; set; }
        public ICommunicationHandler CommunicationHandler { get; }

        public HubconControllerManager(TICommunicationHandler communicationHandler, RequestPipeline requestPipeline)
        {
            Pipeline = requestPipeline;
            CommunicationHandler = communicationHandler;
        }
    }
}
