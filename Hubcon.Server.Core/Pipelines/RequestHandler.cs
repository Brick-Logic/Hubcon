using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;

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

        public async Task<IResponse> HandleWithoutResultAsync(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
            {
                return new BaseOperationResponse(false);
            }

            IOperationContext context = BuildContext(request, blueprint!, cancellationToken);

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return new BaseOperationResponse(pipelineResult.Result!.Success, pipelineResult.Result.Error);
        }

        Task<IOperationResult> ResultHandler(object? result)
        {
            if (result is null)
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse<JsonElement>(true));
            }
            else if (result is IOperationResponse<JsonElement> response)
            {
                return Task.FromResult<IOperationResult>(response);
            }
            else
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse<JsonElement>(true, _converter.SerializeToElement(result)));
            }
        }

        public async Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
            {
                return new BaseJsonResponse(false, default, null);
            }

            IOperationContext context = BuildContext(request, blueprint, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return (IOperationResponse<JsonElement>)pipelineResult.Result!;
        }

        static async Task<IOperationResult> NoResultHandler(object? result)
        {
            if (result is Task task)
                await task;

            else if (result is IResponse response)
                return new BaseOperationResponse<object?>(response.Success, null, response.Error);

            return new BaseOperationResponse<object>(true);
        }

        public async Task<IResponse> HandleSynchronous(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
                return new BaseOperationResponse(false);

            IOperationContext context = BuildContext(request, blueprint, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, NoResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Result!;
        }

        public async Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetStream(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Stream))
                return null!;

            IOperationContext context = BuildContext(request, blueprint, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Result;

            if (res == null)
                return new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, "Internal server error");

            if (!res.Success)
                return new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, res.Error);

            return new BaseOperationResponse<IAsyncEnumerable<object?>?>(true, (IAsyncEnumerable<object?>)res.Data!);
        }

        static Task<IOperationResult> StreamResultHandler(object? result)
        {
            if (result is IAsyncEnumerable<object?> sub)
            {
                return Task.FromResult<IOperationResult>(
                    new BaseOperationResponse<IAsyncEnumerable<object?>?>(true, sub!)
                );
            }
            else if (result is IOperationResult opResult)
            {
                return Task.FromResult(opResult);
            }
            else
            {
                return Task.FromResult<IOperationResult>(
                    new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, "Internal server error")
                );
            }
        }

        public async Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Subscription))
                return new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, "The specified method was not found on the server");

            IOperationContext context = BuildContext(request, blueprint, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, StreamResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Result;

            if (res == null)
                return new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, "Internal server error");

            if (!res.Success)
                return new BaseOperationResponse<IAsyncEnumerable<object?>?>(false, null, res.Error);

            return new BaseOperationResponse<IAsyncEnumerable<object?>?>(true, (IAsyncEnumerable<object?>)res.Data!);
        }

        async Task<IOperationResult> WithResultHandler(object? result)
        {
            if (result is null)
            {
                return new BaseOperationResponse<object>(true);
            }
            else if (result is Task task)
            {
                var response = await GetTaskResultAsync(task);
                return new BaseOperationResponse<object>(true, response!);
            }
            else
            {
                return new BaseOperationResponse<object>(true, result);
            }
        }

        public async Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
                return null!;

            var context = BuildContext(request, blueprint, cancellationToken);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeToElement(pipelineResult.Result.Data), pipelineResult.Result.Error);
        }

        public async Task<IOperationResponse<JsonElement>> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Ingest))
                return null!;

            var count = request.Arguments?.Count + blueprint?.ParameterTypes.Count(x => x.GetType() == typeof(CancellationToken));

            if (request.Arguments?.Count == 0
                || count == 0
                || count != request.Arguments?.Count)
            {
                return new BaseOperationResponse<JsonElement>(false, default, "Parameter count mismatch.");
            }

            var arguments = new List<object?>();
            
            foreach (var parameterType in blueprint!.ParameterTypes)
            {
                object? arg = null;

                if (!request.Arguments!.TryGetValue(parameterType.Key, out arg))
                {
                    continue;
                }

                if (parameterType.Value.IsGenericType && parameterType.Value.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    var id = _converter.DeserializeData<Guid>(arg);

                    if (id == Guid.Empty) continue;

                    var source = sources.TryGetValue(id!, out object? value);

                    request.Arguments![parameterType.Key] = value;
                }
            }

            IOperationContext context = BuildContext(request, blueprint, cancellationToken);

            var pipeline = blueprint.PipelineBuilder.Build(request, context, WithResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeToElement(pipelineResult.Result.Data), pipelineResult.Result.Error);
        }

        private IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint, CancellationToken cancellationToken)
        {
            return new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                Request = request,
                RequestAborted = cancellationToken,
                // Ejemplo: durante inicialización del blueprint
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