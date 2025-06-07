using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Interfaces
{
    public interface IUnsubscriber
    {
        Task Unsubscribe(IRequest request);
    }
}
