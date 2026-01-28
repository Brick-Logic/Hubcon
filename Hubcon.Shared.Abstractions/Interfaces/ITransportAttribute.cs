using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public interface ITransportAttribute
    {
        public string TransportKey { get; }
    }
}
