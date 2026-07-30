using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public class Snapshot
    {
        public required long Calls { get; init; }
        public required long Invokes { get; init; }
        public required long StreamingsRequests { get; init; }
        public required long IngestsRequests { get; init; }
    }

    /// <summary>
    /// Defines a point-in-time snapshot of the request throughput across the system.
    /// Provides granular metrics categorized by transport protocol and Hubcon operation type.
    /// </summary>
    public interface IRequestsPerSecondSnapshot
    {
        IReadOnlyDictionary<HubconTransportAttribute, Snapshot> Snapshots { get; }
    }
}