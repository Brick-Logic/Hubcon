using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Models;

using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Hubcon.Server.Core.Pipelines
{
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

        public async Task<IHubconResponse> HandleWithoutResultAsync(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint!, wrappedRequest, cancellationToken);

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response;
        }

        Task<IHubconResponse> ResultHandler(object? result)
        {
            if (result is null)
            {
                return Task.FromResult(HubconResponse.Ok());
            }
            else
            {
                return Task.FromResult(HubconResponse.Ok(result));
            }
        }

        public async Task<IHubconResponse> HandleSynchronousResult(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.InvokeMethod))
            {
                return HubconResponse.NotFound();
            }

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        static async Task<IHubconResponse> NoResultHandler(object? result)
        {
            if (result is Task task)
                await task;

            return HubconResponse.Ok();
        }

        public async Task<IHubconResponse> HandleSynchronous(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.CallMethod))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Response!;
        }

        public async Task<IHubconResponse> GetStream(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Stream))
                return null!;

            IOperationContext context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Response;

            if (res == null)
                return HubconResponse.InternalError();

            return res;
        }

        static Task<IHubconResponse> StreamResultHandler(object? result)
        {
            if (result is IAsyncEnumerable<object?> sub)
            {
                return Task.FromResult(HubconResponse.Ok(sub));
            }
            else
            {
                return Task.FromResult(HubconResponse.InternalError());
            }
        }

        public async Task<IHubconResponse> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Subscription))
                return HubconResponse.NotFound();

            IOperationContext context = BuildContext(request, blueprint, null, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Response;

            if (res == null)
                return HubconResponse.InternalError();

            return res;
        }

        async Task<IHubconResponse> WithResultHandler(object? result)
        {
            if (result is Task task)
            {
                var response = await GetTaskResultAsync(task);
                return HubconResponse.Ok(response);
            }
            else
            {
                return HubconResponse.Ok(result);
            }
        }

        public async Task<IHubconResponse> HandleWithResultAsync(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.InvokeMethod))
                return null!;

            var context = BuildContext(request, blueprint, wrappedRequest, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        public async Task<IHubconResponse> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Ingest))
                return null!;

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

            var pipeline = blueprint.PipelineBuilder.Build(request, context, WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return pipelineResult.Response;
        }

        private IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint, object? wrappedRequest, CancellationToken cancellationToken = default)
        {
            return new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                Request = request,
                WrappedRequest = wrappedRequest,
                RequestAborted = cancellationToken,
            };
        }

        private static async Task<object?> GetTaskResultAsync(Task taskObject)
        {
            await taskObject;

            var taskType = taskObject.GetType();

            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                var result = resultProperty?.GetValue(taskObject);

                return result;
            }

            return null;
        }
    }
}