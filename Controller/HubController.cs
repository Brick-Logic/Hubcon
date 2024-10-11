using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Hubcon.Extensions;
using System.Diagnostics;
using Hubcon.Response;

namespace Hubcon.Controller
{
    public record class MethodInvokeInfo(string MethodName, object?[] Args);

    public abstract class HubController
    {
        protected ConcurrentDictionary<string, Delegate?> AvailableMethods = new();

        protected HubConnection _hubConnection;
        public HubConnection Connection => _hubConnection;

        protected HubController(string url)
        {
            _hubConnection = new HubConnectionBuilder()
             .WithUrl(url)
             .WithAutomaticReconnect()
             .Build();

            Build();
        }

        private void Build()
        {
            Type derivedType = GetType();
            if (!typeof(IHubController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IHubController)} o un tipo derivado.");

            var interfaces = derivedType.GetInterfaces();

            foreach (var item in interfaces)
            {
                foreach (var method in item.GetMethods())
                {
                    var parameters = method.GetParameters();
                    var parameterExpressions = parameters.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();

                    var callExpression = method.ReturnType == typeof(void) ?
                        Expression.Call(Expression.Constant(this), method, parameterExpressions) :
                        (Expression)Expression.Call(Expression.Constant(this), method, parameterExpressions);

                    Type delegateType = method.ReturnType == typeof(void) ?
                        Expression.GetActionType(parameters.Select(p => p.ParameterType).ToArray()) :
                        Expression.GetFuncType(parameters.Select(p => p.ParameterType).Concat([method.ReturnType]).ToArray());

                    var lambda = Expression.Lambda(delegateType, callExpression, parameterExpressions);
                    Delegate? action = lambda.Compile();

                    AvailableMethods.TryAdd($"{method.GetMethodSignature()}", action);

                    if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
                        _hubConnection.On($"{method.GetMethodSignature()}", (Func<MethodInvokeInfo, Task<MethodResponse>>)HandleWithResult);
                    else
                        _hubConnection.On($"{method.GetMethodSignature()}", (Func<MethodInvokeInfo, Task>)Handle);
                }
            }
        }

        private async Task Handle(MethodInvokeInfo methodInfo)
        {
            AvailableMethods.TryGetValue(methodInfo.MethodName, out Delegate? value);
            object? result = value?.DynamicInvoke(methodInfo.Args);

            if (result is Task task)
                await task;
        }

        private async Task<MethodResponse> HandleWithResult(MethodInvokeInfo methodInfo)
        {
            AvailableMethods.TryGetValue(methodInfo.MethodName, out Delegate? value);
            object? result = value?.DynamicInvoke(methodInfo.Args);

            if (result is null)
                return new MethodResponse(true);
            else if (result is Task task)
            {
                var response = await GetTaskResultAsync(task, value!.Method.ReturnType.GetGenericArguments()[0]);
                return new MethodResponse(true, response);
            }
            else
                return new MethodResponse(true, result);
        }

        private static async Task<object?> GetTaskResultAsync(Task taskObject, Type returnType)
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
