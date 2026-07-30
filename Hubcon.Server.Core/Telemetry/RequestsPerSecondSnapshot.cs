using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace Hubcon.Server.Core.Telemetry
{
    public sealed record RequestsPerSecondSnapshot(IReadOnlyDictionary<HubconTransportAttribute, Snapshot> Snapshots) : IRequestsPerSecondSnapshot
    {
    }
}