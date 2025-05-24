using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconClientControllerManager<TICommunicationHandler> : IHubconControllerManager<TICommunicationHandler>
     where TICommunicationHandler : ICommunicationHandler
    {
        public new TICommunicationHandler CommunicationHandler { get; }
    }
}
