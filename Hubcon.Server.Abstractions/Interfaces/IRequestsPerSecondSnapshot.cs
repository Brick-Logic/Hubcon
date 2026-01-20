using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IRequestsPerSecondSnapshot
    {
        int SubscriptionsPerSecond { get; }
        int StreamingsPerSecond { get; }
        int IngestsPerSecond { get; }
        int RequestsPerSecond { get; }
        int WebSocketsRequestsPerSecond { get; }
        int WebSocketsRoundTripRequestsPerSecond { get; }
        int HttpCallRequestsPerSecond { get; }
        int HttpRoundTripRequestsPerSecond { get; }
        int WebSocketsCallRequestsPerSecond { get; }
        int HttpRequestsPerSecond { get; }
    }
}
