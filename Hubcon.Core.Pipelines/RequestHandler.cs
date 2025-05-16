using GreenDonut;
using HotChocolate.Language;
using Hubcon.Core.Abstractions;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Exceptions;
using Hubcon.Core.Extensions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Pipelines.UpgradedPipeline;
using Hubcon.Core.Subscriptions;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Core.Pipelines
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequestHandler : IRequestHandler
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
            if (!_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint))
                return new BaseOperationResponse(false);

            IOperationContext context = BuildContext(request, blueprint!);

            static async Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is Task task)
                    await task;

                return new BaseOperationResponse(true);
            }

            var pipeline = blueprint!.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Result!;
        }

        public async Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Method))
                return new BaseJsonResponse(false);

            var controller = _serviceProvider.GetRequiredService(blueprint!.ControllerType);

            IOperationContext context = BuildContext(request, blueprint);

            Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is null)
                {
                    return Task.FromResult<IOperationResult>(new BaseOperationResponse(true));
                }

                return Task.FromResult<IOperationResult>(new BaseOperationResponse(true, _converter.SerializeObject(result)));
            }

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeObject(pipelineResult.Result.Data));
        }

        public async Task<IResponse> HandleSynchronous(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Method))
                return new BaseOperationResponse(false);

            IOperationContext context = BuildContext(request, blueprint);

            static async Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is Task task)
                    await task;

                return new BaseOperationResponse(true);
            }

            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();
            return pipelineResult.Result!;
        }

        public IAsyncEnumerable<JsonElement?> GetStream(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Method))
                return null!;

            static Task<IOperationResult> ResultHandler(object? result)
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse(true, result));
            }

            IOperationContext context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            pipelineTask.Wait();
            var pipelineResult = (IAsyncEnumerable<object>)pipelineTask.Result.Result!.Data!;

            return _converter.ConvertToJsonElementStream(pipelineResult);
        }

        public IAsyncEnumerable<JsonElement?> GetSubscription(
            IOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Subscription))
                throw new EntryPointNotFoundException();

            static Task<IOperationResult> ResultHandler(object? result)
            {
                return Task.FromResult<IOperationResult>(new BaseOperationResponse(true, result));
            }

            IOperationContext context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineTask = pipeline.Execute();
            pipelineTask.Wait();
            return (IAsyncEnumerable<JsonElement?>)pipelineTask.Result.Result!.Data!;
        }


        public async Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request)
        {
            if (!(_operationRegistry.GetOperationBlueprint(request, out IOperationBlueprint? blueprint) && blueprint?.Kind == OperationKind.Method))
                return null!;

            var controller = _serviceProvider.GetRequiredService(blueprint!.ControllerType);


            async Task<IOperationResult> ResultHandler(object? result)
            {
                if (result is null)
                {
                    return new BaseOperationResponse(true);
                }
                else if (result is Task task)
                {
                    var response = await GetTaskResultAsync(task, blueprint!.RawReturnType.GetGenericArguments()[0]);
                    return new BaseOperationResponse(true, _converter.SerializeObject(response));
                }
                else
                {
                    return new BaseOperationResponse(true, _converter.SerializeObject(result));
                }
            };

            var context = BuildContext(request, blueprint);
            var pipeline = blueprint.PipelineBuilder.Build(request, context, ResultHandler, _serviceProvider);
            var pipelineResult = await pipeline.Execute();

            return new BaseJsonResponse(pipelineResult.Result!.Success, _converter.SerializeObject(pipelineResult.Result.Data));
        }

        public IOperationContext BuildContext(IOperationRequest request, IOperationBlueprint blueprint)
        {
            return new OperationContext()
            {
                OperationName = request.OperationName,
                RequestServices = _serviceProvider,
                Blueprint = blueprint,
                Arguments = _converter.DeserializeJsonArgs(request.Args, blueprint!.ParameterTypes).ToArray(),
                HttpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext,
                Request = request
            };
        }

        public static async Task<object?> GetTaskResultAsync(Task taskObject, Type returnType)
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

                return Convert.ChangeType(result, returnType);
            }

            // Si no es un Task<T>, no hay valor que devolver
            return null;
        }
    }
}
