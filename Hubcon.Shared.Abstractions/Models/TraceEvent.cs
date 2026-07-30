using System;
using System.Runtime.InteropServices;

namespace Hubcon.Shared.Abstractions.Models
{
    /// <summary>
    /// Tracing event for telemetry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct TraceEvent<TData>
    {
        public RequestId RequestId { get; }
        public HubconTransportAttribute TransportType { get; }
        
        public readonly long StartTimestamp;
        public readonly long ElapsedTicks;
    
        public readonly TData Data;
    
        public readonly byte Status;
        public readonly Exception? Exception;

        public TraceEvent(
            RequestId requestId,
            TData data,
            HubconTransportAttribute transportType,
            long startTimestamp,
            long elapsedTicks,
            byte status,
            Exception? exception = null)
        {
            RequestId = requestId;
            TransportType = transportType;
            StartTimestamp = startTimestamp;
            ElapsedTicks = elapsedTicks;
            Data = data;
            Status = status;
            Exception = exception;
        }
    }
}

