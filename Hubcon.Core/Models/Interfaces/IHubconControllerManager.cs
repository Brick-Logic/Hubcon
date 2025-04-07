using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IHubconControllerManager
    {
        // Handlers
        public IRequestPipeline Pipeline { get; set; }
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
}
