using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IServerConnectorInterceptor<T>
    {
        ICommunicationHandler CommunicationHandler { get; }
    }
}
