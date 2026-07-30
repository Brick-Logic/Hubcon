using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hubcon.Core.Telemetry;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Telemetry;
using Hubcon.Shared.Abstractions.Models;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares;

public sealed class OpenTelemetryMiddleware : ITelemetryMiddleware
{
    private readonly TelemetryChannelPipeline<IOperationBlueprint> _pipeline;
    private readonly OpenTelemetryBatchWorker _batchWorker;

    public OpenTelemetryMiddleware()
    {
        _pipeline = new TelemetryChannelPipeline<IOperationBlueprint>();
        _batchWorker = new OpenTelemetryBatchWorker(_pipeline);
        _ = _batchWorker.ExecuteAsync();
    }
    
    /// <inheritdoc/>
    public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        byte status = 0;

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            status = 1; // 1 = Error
            context.Exception = ex;
        }
        finally
        {
            long elapsedTicks = Stopwatch.GetElapsedTime(startTimestamp).Ticks;

            var traceEvent = new TraceEvent<IOperationBlueprint>(
                context.RequestId,
                context.Blueprint,
                context.TransportType,
                startTimestamp,
                elapsedTicks,
                status,
                context.Exception
            );

            _pipeline.Emit(in traceEvent);
        }
    }
}