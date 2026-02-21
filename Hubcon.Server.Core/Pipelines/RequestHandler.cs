using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.ResultHandlers;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Security.Claims;

namespace Hubcon.Server.Core.Pipelines
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RequestHandler : IRequestHandler
    {
        private readonly IOperationRegistry _operationRegistry;
        private readonly IDynamicConverter _converter;
        private readonly IInternalServerOptions internalServerOptions;
        private readonly IServiceProvider _serviceProvider;

        public RequestHandler(
            IOperationRegistry operationRegistry,
            IDynamicConverter dynamicConverter,
            IInternalServerOptions internalServerOptions,
            IServiceProvider serviceProvider)
        {
            _operationRegistry = operationRegistry;
            _converter = dynamicConverter;
            this.internalServerOptions = internalServerOptions;
            _serviceProvider = serviceProvider;
        }

        public async Task<IHubconResponse> HandleWithoutResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint!, wrappedRequest, cancellationToken);

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, PipelineResultHandlers.NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response;
        }

        public async Task<IHubconResponse> HandleSynchronousResult(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.InvokeMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        public async Task<IHubconResponse> HandleSynchronous(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response!;
        }

        public async Task<IHubconResponse> GetStream(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Stream))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Response;

            if (res == null)
                return HubconResponse.InternalError();

            return res;
        }

        public async Task<IHubconResponse> GetSubscription(IOperationRequest request, HubconTransportAttribute transportAttribute, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Subscription))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, null, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Response;

            if (res == null)
                return HubconResponse.InternalError();

            return res;
        }

        public async Task<IHubconResponse> HandleWithResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.InvokeMethod))
                return HubconResponse.NotFound();

            var context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        public async Task<IHubconResponse> HandleIngest(IOperationRequest request, HubconTransportAttribute transportAttribute, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, transportAttribute, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Ingest))
                return HubconResponse.NotFound();

            var dict = request.Arguments.ToDictionary();

            var count = dict?.Count + blueprint?.ParameterTypes.Count(x => x.GetType() == typeof(CancellationToken));

            if (dict?.Count == 0
                || count == 0
                || count != dict?.Count)
            {
                return HubconResponse.InternalError();
            }

            var arguments = new List<object?>();

            foreach (var parameterType in blueprint!.ParameterTypes)
            {
                object? arg = null;

                if (!dict!.TryGetValue(parameterType.Key, out arg))
                {
                    continue;
                }

                if (parameterType.Value.IsGenericType && parameterType.Value.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    var id = _converter.DeserializeData<Guid>(arg);

                    if (id == Guid.Empty) continue;

                    var source = sources.TryGetValue(id!, out object? value);

                    dict![parameterType.Key] = value!;
                }
            }

            PropertyTools.AssignProperty(request, nameof(request.Arguments), dict);
            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, PipelineResultHandlers.WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        private IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            var context = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
            (ClaimsPrincipal? user, DateTime? expiration)? user = null;
            if (blueprint.RequiresAuthorization)
            {
                try
                {
                    user = internalServerOptions.TokenHandler?.Invoke(JwtHelper.ExtractTokenFromHeader(context)!, context!.RequestServices);
                }
                finally
                {
                }
            }

            return new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                Request = request,
                User = user.HasValue ? user.Value.user : null,
                WrappedRequest = wrappedRequest,
                RequestAborted = cancellationToken,
            };
        }
    }
}