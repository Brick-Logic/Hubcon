using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Enums
{
    public enum SubscriptionState
    {
        Connected,
        Disconnected,
        Reconnecting,
        Emitter
    }
}
