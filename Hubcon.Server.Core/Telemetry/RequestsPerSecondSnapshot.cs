using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Telemetry
{
    public sealed record RequestsPerSecondSnapshot : IRequestsPerSecondSnapshot
    {
        public required int SubscriptionsPerSecond { get; init; }
        public required int StreamingsPerSecond { get; init; }
        public required int IngestsPerSecond { get; init; }
        public required int WebSocketsRequestsPerSecond { get; init; }
        public required int WebSocketsCallRequestsPerSecond { get; init; }
        public required int WebSocketsRoundTripRequestsPerSecond { get; init; }
        public required int HttpRequestsPerSecond { get; init; }
        public required int HttpCallRequestsPerSecond { get; init; }
        public required int HttpRoundTripRequestsPerSecond { get; init; }
        public required int RequestsPerSecond { get; init; }
    }
}
