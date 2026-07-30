using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hubcon.Core.Telemetry;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.Extensions.Hosting;

namespace Hubcon.Server.Core.Telemetry;

public sealed class OpenTelemetryBatchWorker
{
    private readonly TelemetryChannelPipeline<IOperationBlueprint> _pipeline;
    private readonly List<TraceEvent<IOperationBlueprint>> _batchBuffer;
    private const int MaxBatchSize = 50_000;

    // ActivitySource de OpenTelemetry
    public static ActivitySource HubconActivitySource { get; } = new("Hubcon", "1.0.0");

    public OpenTelemetryBatchWorker(TelemetryChannelPipeline<IOperationBlueprint> pipeline)
    {
        _pipeline = pipeline;
        _batchBuffer = new List<TraceEvent<IOperationBlueprint>>(MaxBatchSize);
    }

    public async Task ExecuteAsync()
    {
        var reader = _pipeline.Reader;

        while (await reader.WaitToReadAsync(CancellationToken.None))
        {
            while (_batchBuffer.Count < MaxBatchSize && reader.TryRead(out var traceEvent))
            {
                _batchBuffer.Add(traceEvent);
            }

            if (_batchBuffer.Count == 0)
                continue;

            ProcessAndExportBatch(_batchBuffer);

            _batchBuffer.Clear();
        }
    }

    private void ProcessAndExportBatch(List<TraceEvent<IOperationBlueprint>> batch)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            ref readonly var evt = ref CollectionsMarshal.AsSpan(batch)[i];

            var traceId = evt.RequestId.ToActivityTraceId();

            using var activity = HubconActivitySource.StartActivity(
                name: $"Endpoint/{evt.Data.SimpleContractName}/{evt.Data.MemberInfo!.Name}",
                kind: ActivityKind.Server,
                parentContext: new ActivityContext(traceId, default, ActivityTraceFlags.Recorded),
                null
            );

            if (activity != null)
            {
                activity.SetTag("rpc.system", "Hubcon");
                activity.SetTag("rpc.transport", evt.TransportType.GetType().Name);
                activity.SetTag("rpc.operation_kind", evt.Data.Kind);
                activity.SetTag("rpc.method", evt.Data.MemberInfo!.Name);
                activity.SetTag("rpc.service", evt.Data.SimpleContractName);
                activity.SetTag("rpc.full_contract_name", evt.Data.ContractName);
                activity.SetTag("rpc.contract_handler_name", evt.Data.ControllerName);
                activity.SetTag("rpc.requires_auth", evt.Data.RequiresAuthorization ? 1 : 0);
                
                var duration = TimeSpan.FromTicks((long)(evt.ElapsedTicks * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
                activity.SetEndTime(activity.StartTimeUtc + duration);

                if (evt.Status != 0)
                {
                    activity.SetStatus(ActivityStatusCode.Error, evt.Exception?.Message);
                }
            }
        }
    }
}