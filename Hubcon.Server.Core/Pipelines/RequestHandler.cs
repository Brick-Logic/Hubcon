#pragma warning disable CS1591
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.ResultHandlers;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Server.Core.Pipelines
{
    public static class OperationContextProvider
    {
        private static AsyncLocal<IOperationContext?> CurrentOperationContext { get; } = new();
        private static AsyncLocal<bool> ContextIsSet { get; } = new();

        public static void SetContext(IOperationContext context)
        {
            if (ContextIsSet.Value == true)
                return;

            ContextIsSet.Value = true;
            CurrentOperationContext.Value = context;
        }

        public static IOperationContext? GetContext() => CurrentOperationContext.Value;

        public static void ClearContext() => CurrentOperationContext.Value = null;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RequestHandler : IRequestHandler
    {
        private readonly IOperationRegistry _operationRegistry;
        private readonly IDynamicConverter _converter;
        private readonly IServiceProvider _serviceProvider;

        public RequestHandler(
            IOperationRegistry operationRegistry,
            IDynamicConverter dynamicConverter,
            IServiceProvider serviceProvider)
        {
            _operationRegistry = operationRegistry;
            _converter = dynamicConverter;
            _serviceProvider = serviceProvider;
        }

        public async ValueTask<IResponse> HandleWithoutResultAsync(IOperationRequest request,
            HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                    out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint!, PipelineResultHandlers.NoResultHandler,
                wrappedRequest, requestId, transportAttribute, cancellationToken);

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response;
        }

        public async ValueTask<IResponse> HandleSynchronousResult(IOperationRequest request,
            HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                      out IOperationBlueprint? blueprint)
                  && blueprint?.Kind == OperationKind.InvokeMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint, PipelineResultHandlers.ResultHandler,
                wrappedRequest, requestId, transportAttribute, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        public async ValueTask<IResponse> HandleSynchronous(IOperationRequest request,
            HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                    out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, PipelineResultHandlers.NoResultHandler,
                wrappedRequest, requestId, transportAttribute, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response!;
        }

        public async ValueTask<IResponse> GetStream(IOperationRequest request,
            HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                    out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Stream))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, PipelineResultHandlers.StreamResultHandler,
                wrappedRequest, requestId, transportAttribute, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Response;

            if (res == null)
                return HubconResponse.InternalError();

            return res;
        }

        public async ValueTask<IResponse> HandleWithResultAsync(IOperationRequest request,
            HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                    out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.InvokeMethod))
                return HubconResponse.NotFound();

            var context = BuildContext(request, blueprint, PipelineResultHandlers.WithResultHandler, wrappedRequest,
                requestId, transportAttribute, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        public async ValueTask<IResponse> HandleIngest(IOperationRequest request,
            HubconTransportAttribute transportAttribute, Dictionary<Guid, object> sources, IWrapper? wrappedRequest,
            RequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.TryGetOperationBlueprint(request, transportAttribute,
                    out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Ingest))
                return HubconResponse.NotFound();

            var dict = request.Arguments.ToDictionary();

            var count = dict?.Count + blueprint?.ParameterTypes.Count(x => x.GetType() == typeof(CancellationToken));

            if (dict?.Count == 0
                || count == 0
                || count != dict?.Count)
            {
                return HubconResponse.InternalError();
            }

            foreach (var parameterType in blueprint!.ParameterTypes)
            {
                object? arg = null;

                if (!dict!.TryGetValue(parameterType.Key, out arg))
                {
                    continue;
                }

                if (parameterType.Value.IsGenericType &&
                    parameterType.Value.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    var id = _converter.DeserializeData<Guid>(arg);

                    if (id == Guid.Empty) continue;

                    sources.TryGetValue(id!, out var value);

                    dict![parameterType.Key] = value!;
                }
            }

            request.AssignArguments(dict!);

            ResultHandlerDelegate resultHandler = blueprint.HasReturnType
                ? PipelineResultHandlers.WithResultHandler
                : PipelineResultHandlers.NoResultHandler;

            var context = BuildContext(request, blueprint, resultHandler, wrappedRequest, requestId, transportAttribute, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        private IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint,
            ResultHandlerDelegate resultHandler, IWrapper? wrappedRequest, RequestId requestId, HubconTransportAttribute transportAttribute,
            CancellationToken cancellationToken = default)
        {
            var context = new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                RequestId = requestId,
                Request = request,
                WrappedRequest = wrappedRequest,
                TransportType = transportAttribute,
                ResultHandler = resultHandler,
                RequestAborted = cancellationToken
            };

            return context;
        }
    }
}