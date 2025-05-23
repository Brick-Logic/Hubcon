using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
