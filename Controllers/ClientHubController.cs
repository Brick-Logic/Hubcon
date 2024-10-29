using Hubcon.Connectors;
using Hubcon.Extensions;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Controllers
{
    public class MethodHandler
    {
        internal ConcurrentDictionary<string, Delegate?> AvailableMethods = new();

        public void BuildMethods(object instance, Type type, Action<string, MethodInfo, Delegate>? forEachMethodAction = null)
        {
            var interfaces = type.GetInterfaces().Where(x => typeof(IHubController).IsAssignableFrom(x));

            foreach (var item in interfaces)
            {
                if (item.GetMethods().Length == 0)
                    continue;

                foreach (var method in item.GetMethods())
                {
                    var parameters = method.GetParameters();
                    var parameterExpressions = parameters.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();

                    var callExpression = method.ReturnType == typeof(void) ?
                        Expression.Call(Expression.Constant(instance), method, parameterExpressions) :
                        (Expression)Expression.Call(Expression.Constant(instance), method, parameterExpressions);

                    Type delegateType = method.ReturnType == typeof(void) ?
                        Expression.GetActionType(parameters.Select(p => p.ParameterType).ToArray()) :
                        Expression.GetFuncType(parameters.Select(p => p.ParameterType).Concat([method.ReturnType]).ToArray());

                    var lambda = Expression.Lambda(delegateType, callExpression, parameterExpressions);
                    Delegate? action = lambda.Compile();

                    var methodSignature = method.GetMethodSignature();

                    AvailableMethods.TryAdd($"{methodSignature}", action);

                    forEachMethodAction?.Invoke(methodSignature, method, action);
                }
            }
        }
    
        public async Task HandleWithoutResultAsync(MethodInvokeInfo methodInfo)
        {
            AvailableMethods.TryGetValue(methodInfo.MethodName, out Delegate? value);
            object? result = value?.DynamicInvoke(methodInfo.Args);

            if (result is Task task)
                await task;
        }

        public async Task<MethodResponse> HandleSynchronousResult(MethodInvokeInfo methodInfo)
        {
            return await Task.Run(() =>
            {
                AvailableMethods.TryGetValue(methodInfo.MethodName, out Delegate? value);
                object? result = value?.DynamicInvoke(methodInfo.Args);

                if (result is null)
                    return new MethodResponse(true);

                return new MethodResponse(true, result);
            });
        }

        public Task HandleSynchronous(MethodInvokeInfo methodInfo)
        {
            AvailableMethods.TryGetValue(methodInfo.MethodName, out Delegate? value);
            value?.DynamicInvoke(methodInfo.Args);

            return Task.CompletedTask;
        }

        public async Task<MethodResponse> HandleWithResultAsync(MethodInvokeInfo methodInfo)
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
    public class HubController : IHubController
    {
        protected MethodHandler handler;

        public HubController()
        {
            handler = new MethodHandler();           
        }

        protected void Build(HubConnection hubConnection)
        {

            Type derivedType = GetType();
            if (!typeof(IHubController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IHubController)} o un tipo derivado.");

            handler.BuildMethods(this, derivedType, (methodSignature,methodInfo, delegado) =>
            {
                if (methodInfo.ReturnType == typeof(void))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task>)handler.HandleWithoutResultAsync);
                else if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task<MethodResponse>>)handler.HandleWithResultAsync);
                else if (methodInfo.ReturnType == typeof(Task))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task>)handler.HandleWithoutResultAsync);
                else
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task<MethodResponse>>)handler.HandleWithResultAsync);
            });                         
        }
    }
    public class ClientHubController : HubController
    {
        protected HubConnection _hubConnection;
        protected CancellationToken _token;
        protected string _url;

        protected ClientHubController(string url)
        {
            _url = url;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();

            Build(_hubConnection);
        }

        public TIServerHubController GetConnector<TIServerHubController>() where TIServerHubController : IServerHubController
        {
            return new ServerHubControllerConnector<TIServerHubController>(_hubConnection).Instance;
        }

        public async Task StartAsync(Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _token = cancellationToken;

                _ = _hubConnection.StartAsync(_token);

                bool connectedInvoked = false;
                while (true)
                {
                    await Task.Delay(1000, cancellationToken);
                    if (_hubConnection.State == HubConnectionState.Connecting)
                    {
                        consoleOutput?.Invoke($"Connecting to {_url}...");
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Disconnected)
                    {
                        consoleOutput?.Invoke($"Disconnected. Trying connecting to {_url}...");
                        _ = _hubConnection.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Reconnecting)
                    {
                        consoleOutput?.Invoke($"Connection lost, reconnecting to {_url}...");
                        _ = _hubConnection.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Connected && !connectedInvoked)
                    {
                        consoleOutput?.Invoke($"Successfully connected to {_url}.");
                        connectedInvoked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                consoleOutput?.Invoke($"Error: {ex.Message}");

                if (_token.IsCancellationRequested)
                {
                    consoleOutput?.Invoke("Cancelado.");
                }
            }

            _ = _hubConnection?.StopAsync(_token);
        }
        public void Stop()
        {
            _ = _hubConnection?.StopAsync(_token);
        } 
    }

    public class ClientHubController<TIServerHubController> : ClientHubController
        where TIServerHubController : IServerHubController
    {
        public TIServerHubController Server { get; private set; }
        public ClientHubController(string url) : base(url) => Server = GetConnector<TIServerHubController>();
    }
}
