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

        public async Task<IResponse> HandleWithoutResultAsync(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
            {
                return new BaseOperationResponse(false);
            }

            IOperationContext context = BuildContext(request, blueprint!);

            static async Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is Task task)
                    await task;

                return new BaseOperationResponse<object>(true);
            }

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return new BaseOperationResponse(pipelineResult.Result!.Success, pipelineResult.Result.Error);
        }

        public async Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
            {
                return new BaseJsonResponse(false, default, null);
            }

            IOperationContext context = BuildContext(request, blueprint);

            Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is null)
                {
                    return Task.FromResult<IOperationResult>(new BaseOperationResponse<JsonElement>(true));
                }

                return Task.FromResult<IOperationResult>(new BaseOperationResponse<JsonElement>(true, _converter.SerializeToElement(result)));
            }

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeToElement(pipelineResult.Result.Data), null);
        }

        public async Task<IResponse> HandleSynchronous(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
                return new BaseOperationResponse(false);

            IOperationContext context = BuildContext(request, blueprint);

            static async Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is Task task)
                    await task;

                return new BaseOperationResponse<object>(true);
            }

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Result!;
        }

        public async Task<IAsyncEnumerable<object?>> GetStream(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Stream))
                return null!;

            static Task<IOperationResult> ResultHandler(object? result)
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse<object>(true, result));
            }

            IOperationContext context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;

            return (IAsyncEnumerable<object>)pipelineTask.Result.Result!.Data!;
        }

        public async Task<IAsyncEnumerable<object?>> GetSubscription(
            IOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Subscription))
                throw new EntryPointNotFoundException();

            static Task<IOperationResult> ResultHandler(object? result)
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse<object>(true, result));
            }

            IOperationContext context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            await pipelineTask;
            var res = pipelineTask.Result.Result!.Data!;
            return (IAsyncEnumerable<object?>)res;
        }


        public async Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint)
                && blueprint?.Kind == OperationKind.Method))
                return null!;

            async Task<IOperationResult> ResultHandler(object? result)
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
            ;

            var context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeToElement(pipelineResult.Result.Data), null);
        }

        public async Task<IResponse> HandleIngest(IOperationRequest request, Dictionary<string, object> sources)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Ingest))
                return null!;

            if (request.Arguments?.Count() == 0
                || blueprint?.ParameterTypes.Count == 0
                || blueprint?.ParameterTypes.Count != request.Arguments?.Count())
            {
                return new BaseOperationResponse(false);
            }

            var arguments = new List<object?>();

            
            foreach (var parameterType in blueprint!.ParameterTypes)
            {
                var arg = request.Arguments?[parameterType.Key];

                if (parameterType.Value.IsGenericType && parameterType.Value.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    var id = _converter.DeserializeData<string>(arg);

                    if (id == null) continue;

                    var source = sources.TryGetValue(id!, out object? value);

                    request.Arguments![parameterType.Key] = value;
                }
            }

            IOperationContext context = BuildContext(request, blueprint);

            static async Task<IOperationResult> ResultHandler(object? result)
            {
                try
                {
                    if (result is Task task)
                        await task;

                    return new BaseOperationResponse<object>(true);
                }
                catch (Exception ex)
                {
                    return new BaseOperationResponse<object>(false);
                }
            }

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Result!;
        }

        public IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint)
        {
            return new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                Request = request
            };
        }

        public static async Task<object?> GetTaskResultAsync(Task taskObject)
        {
            // Esperar a que el Task termine
            await taskObject;

            // Verificar si es un Task<T> (Task con resultado)
            var taskType = taskObject.GetType();

            if (taskType.IsGenericType)
            {
                // Obtener el tipo del resultado (T)
                var resultProperty = taskType.GetProperty("Result");

                // Obtener el resultado del Task
                var result = resultProperty?.GetValue(taskObject);

                return result;
            }

            // Si no es un Task<T>, no hay valor que devolver
            return null;
        }
    }
}