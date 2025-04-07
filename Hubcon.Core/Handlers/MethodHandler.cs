using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Core.Handlers
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequestPipeline : IRequestPipeline
    {
        public async Task HandleWithoutResultAsync(object instance, MethodInvokeRequest methodInfo)
        {
            Console.WriteLine($"[MethodHandler] Received call {methodInfo.MethodName}. Args: [{string.Join(",", methodInfo.Args.Select(x => $"{x}"))}]");

            MethodInvokerProvider.GetMethodInvoker(methodInfo.MethodName, instance.GetType(), out var methodInvoker);
            object? result = methodInvoker?.Method?.DynamicInvoke(instance, methodInfo.GetDeserializedArgs(methodInvoker!.ParameterTypes));

            if (result is Task task)
                await task;
        }

        public async Task<MethodResponse> HandleSynchronousResult(object instance, MethodInvokeRequest methodInfo)
        {
            Console.WriteLine($"[MethodHandler] Received call {methodInfo.MethodName}. Args: [{string.Join(",", methodInfo.Args.Select(x => $"{x}"))}]");

            return await Task.Run(() =>
            {
                MethodInvokerProvider.GetMethodInvoker(methodInfo.MethodName, instance.GetType(), out var methodInvoker);
                object? result = methodInvoker?.Method?.DynamicInvoke(instance, methodInfo.GetDeserializedArgs(methodInvoker!.ParameterTypes));

                if (result is null)
                    return new MethodResponse(true);

                return new MethodResponse(true, result).SerializeData();
            });
        }

        public Task HandleSynchronous(object instance, MethodInvokeRequest methodInfo)
        {
            Console.WriteLine($"[MethodHandler] Received call {methodInfo.MethodName}. Args: [{string.Join(",", methodInfo.Args.Select(x => $"{x}"))}]");

            return Task.Run(async () =>
            {
                MethodInvokerProvider.GetMethodInvoker(methodInfo.MethodName, instance.GetType(), out var methodInvoker);
                object? result = methodInvoker?.Method?.DynamicInvoke(instance, methodInfo.GetDeserializedArgs(methodInvoker!.ParameterTypes));

                if (result is Task task)
                    await task;
            });
        }

        public IAsyncEnumerable<object> GetStream(object instance, MethodInvokeRequest methodInfo)
        {
            Console.WriteLine($"[MethodHandler] Received call {methodInfo.MethodName}. Args: [{string.Join(",", methodInfo.Args.Select(x => $"{x}"))}]");

            MethodInvokerProvider.GetMethodInvoker(methodInfo.MethodName, instance.GetType(), out var methodInvoker);
            object? result = methodInvoker?.Method?.DynamicInvoke(instance, methodInfo.GetDeserializedArgs(methodInvoker!.ParameterTypes));

            return (IAsyncEnumerable<object>)result!;          
        }

        public async Task<MethodResponse> HandleWithResultAsync(object instance, MethodInvokeRequest methodInfo)
        {
            Console.WriteLine($"[MethodHandler] Received call {methodInfo.MethodName}. Args: [{string.Join(",", methodInfo.Args.Select(x => $"{x}"))}]");

            MethodInvokerProvider.GetMethodInvoker(methodInfo.MethodName, instance.GetType(), out var methodInvoker);
            object? result = methodInvoker?.Method?.DynamicInvoke(instance, methodInfo.GetDeserializedArgs(methodInvoker!.ParameterTypes));

            if (result is null)
                return new MethodResponse(true);
            else if (result is Task task)
            {
                var response = await GetTaskResultAsync(task, methodInvoker!.ReturnType.GetGenericArguments()[0]);
                return new MethodResponse(true, response).SerializeData();
            }
            else
                return new MethodResponse(true, result).SerializeData();
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
