using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Enums
{
    public enum TransportType
    {
        Default, // Will decide based on contract configuration, or http by default
        Http,
        Websockets
    }
}
