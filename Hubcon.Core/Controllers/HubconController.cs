using Hubcon.Core.Handlers;
using Hubcon.Core.Interfaces;
using Hubcon.Core.Interfaces.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Controllers
{
    public interface IHubconControllerManager
    {
        // Handlers
        public IMethodHandler Methods { get; set; }
        public ICommunicationHandler CommunicationHandler { get; set; }
    }

    public interface IHubconClientControllerManager : IHubconControllerManager
    {
        public new ICommunicationHandler CommunicationHandler { get; set; }
    }

    public interface IHubconServerControllerManager : IHubconControllerManager
    {
        public new IServerCommunicationHandler CommunicationHandler { get; set; }
    }

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
