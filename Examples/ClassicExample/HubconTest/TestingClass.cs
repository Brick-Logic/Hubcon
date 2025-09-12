using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;

namespace HubconTest
{
    // Controller de ejemplo
    public class MyCustomController
    {
        public async Task<string> GetUser(int id, string name = "default")
        {
            await Task.Delay(10);
            return $"User {id}: {name}";
        }

        public async Task<UserDto> CreateUser([FromQuery] CreateUserRequest request)
        {
            await Task.Delay(10);
            return new UserDto(Random.Shared.Next(1, 1000), request.Name, request.Email);
        }

        public async Task<bool> DeleteUser(int id)
        {
            await Task.Delay(10);
            return id > 0;
        }

        public string GetSimple()
        {
            return "Simple response";
        }
    }

    // DTOs de ejemplo
    public record CreateUserRequest(string Name, string Email);
    public record class UserDto(int Id, string Name, string Email);

    // Sistema de pipeline personalizado
    public class CustomPipeline
    {
        public async Task<T> ExecuteAsync<T>(MethodInfo method, object controller, object[] parameters)
        {
            Console.WriteLine($"🚀 Pipeline ejecutando: {method.Name}");
            Console.WriteLine($"📝 Parámetros: [{string.Join(", ", parameters.Select(p => p?.ToString() ?? "null"))}]");

            try
            {
                // Pre-procesamiento
                await PreProcess(method, parameters);

                // Invocar el método original
                var result = method.Invoke(controller, parameters);

                // Manejar métodos async
                if (result is Task task)
                {
                    await task;
                    if (task.GetType().IsGenericType)
                    {
                        var property = task.GetType().GetProperty("Result");
                        result = property?.GetValue(task);
                    }
                    else
                    {
                        result = default(T); // Task sin retorno
                    }
                }

                // Post-procesamiento
                result = await PostProcess(method, result);

                Console.WriteLine($"✅ Resultado: {result}");
                return (T)result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en pipeline: {ex.Message}");
                throw;
            }
        }

        private async Task PreProcess(MethodInfo method, object[] parameters)
        {
            // Tu lógica de pre-procesamiento aquí
            await Task.Delay(1);
        }

        private async Task<object?> PostProcess(MethodInfo method, object? result)
        {
            // Tu lógica de post-procesamiento aquí
            await Task.Delay(1);
            return result;
        }
    }

    // Mapeador de endpoints simplificado
    public class EndpointMapper
    {
        private readonly WebApplication _app;

        public EndpointMapper(WebApplication app)
        {
            _app = app;
        }

        public void MapController<T>() where T : class
        {
            var controllerType = typeof(T);
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controllerType && !m.IsSpecialName);

            foreach (var method in methods)
            {
                MapMethod<T>(method);
            }
        }

        private void MapMethod<T>(MethodInfo method) where T : class
        {
            var httpMethod = GetHttpMethod(method.Name);
            var route = GetRoute(method.Name);
            var parameters = method.GetParameters();

            Console.WriteLine($"🔗 Mapeando: {httpMethod} {route} -> {method.Name}");

            // Crear delegados específicos para cada caso
            if (parameters.Length == 0)
            {
                MapNoParameters<T>(method, httpMethod, route);
            }
            else if (parameters.Length == 1)
            {
                MapSingleParameter<T>(method, httpMethod, route, parameters[0]);
            }
            else if (parameters.Length == 2)
            {
                MapTwoParameters<T>(method, httpMethod, route, parameters);
            }
            else
            {
                Console.WriteLine($"⚠️  Método {method.Name} tiene demasiados parámetros, no se puede mapear automáticamente");
            }
        }

        private void MapNoParameters<T>(MethodInfo method, string httpMethod, string route) where T : class
        {
            var returnType = GetActualReturnType(method.ReturnType);

            if (returnType == typeof(void))
            {
                // Action sin parámetros ni retorno
                Func<HttpContext, Task> handler = async (context) =>
                {
                    var controller = context.RequestServices.GetRequiredService<T>();
                    var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();

                    await pipeline.ExecuteAsync<object>(method, controller, Array.Empty<object>());
                };

                MapByHttpMethod(httpMethod, route, handler);
            }
            else
            {
                // Func sin parámetros con retorno
                Func<HttpContext, Task<object?>> handler = async (context) =>
                {
                    var controller = context.RequestServices.GetRequiredService<T>();
                    var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();

                    return await pipeline.ExecuteAsync<object>(method, controller, Array.Empty<object>());
                };

                MapByHttpMethod(httpMethod, route, handler);
            }
        }

        private void MapSingleParameter<T>(MethodInfo method, string httpMethod, string route, ParameterInfo param) where T : class
        {
            var paramType = param.ParameterType;
            var returnType = GetActualReturnType(method.ReturnType);

            if (IsSimpleType(paramType))
            {
                // Parámetro simple (int, string, etc.)
                if (returnType == typeof(void))
                {
                    var delegateType = typeof(Func<,>).MakeGenericType(paramType, typeof(Task));
                    var handler = CreateSimpleParameterDelegate<T>(method, paramType, typeof(void));
                    MapByHttpMethod(httpMethod, route, handler);
                }
                else
                {
                    var delegateType = typeof(Func<,,>).MakeGenericType(paramType, typeof(HttpContext), typeof(Task<object>));
                    var handler = CreateSimpleParameterDelegateWithReturn<T>(method, paramType);
                    MapByHttpMethod(httpMethod, route, handler);
                }
            }
            else
            {
                // Parámetro complejo (DTO)
                var delegateType = typeof(Func<,,>).MakeGenericType(paramType, typeof(HttpContext), typeof(Task<object>));
                var handler = CreateComplexParameterDelegate<T>(method, paramType);
                MapByHttpMethod(httpMethod, route, handler);
            }
        }

        private void MapTwoParameters<T>(MethodInfo method, string httpMethod, string route, ParameterInfo[] parameters) where T : class
        {
            var param1Type = parameters[0].ParameterType;
            var param2Type = parameters[1].ParameterType;

            // Delegado para dos parámetros simples
            Func<HttpContext, Task<object?>> handler = async (context) =>
            {
                var controller = context.RequestServices.GetRequiredService<T>();
                var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();

                // Extraer parámetros de query string o route
                var param1Value = GetParameterValue(context, parameters[0]);
                var param2Value = GetParameterValue(context, parameters[1]);

                return await pipeline.ExecuteAsync<object>(method, controller, new[] { param1Value, param2Value });
            };

            MapByHttpMethod(httpMethod, route, handler);
        }

        private Delegate CreateSimpleParameterDelegate<T>(MethodInfo method, Type paramType, Type returnType) where T : class
        {
            if (paramType == typeof(int))
            {
                Func<int, HttpContext, Task> handler = async (param, context) =>
                {
                    var controller = context.RequestServices.GetRequiredService<T>();
                    var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();
                    await pipeline.ExecuteAsync<object>(method, controller, new object[] { param });
                };
                return handler;
            }
            else if (paramType == typeof(string))
            {
                Func<string, HttpContext, Task> handler = async (param, context) =>
                {
                    var controller = context.RequestServices.GetRequiredService<T>();
                    var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();
                    await pipeline.ExecuteAsync<object>(method, controller, new object[] { param });
                };
                return handler;
            }

            throw new NotSupportedException($"Tipo de parámetro {paramType} no soportado");
        }

        private Delegate CreateSimpleParameterDelegateWithReturn<T>(MethodInfo method, Type paramType) where T : class
        {
            if (paramType == typeof(int))
            {
                Func<int, string, HttpContext, Task<object?>> handler = async (id, name, context) =>
                {
                    var controller = context.RequestServices.GetRequiredService<T>();
                    var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();
                    return await pipeline.ExecuteAsync<object>(method, controller, new object[] { id, name ?? "default" });
                };
                return handler;
            }

            throw new NotSupportedException($"Tipo de parámetro {paramType} no soportado");
        }

        private Delegate CreateComplexParameterDelegate<T>(MethodInfo method, Type paramType) where T : class
        {
            // Para objetos complejos que vienen del body
            Func<HttpContext, Task<object?>> handler = async (context) =>
            {
                var controller = context.RequestServices.GetRequiredService<T>();
                var pipeline = context.RequestServices.GetRequiredService<CustomPipeline>();

                // Deserializar el body como el tipo esperado
                var bodyParam = await context.Request.ReadFromJsonAsync(paramType);

                return await pipeline.ExecuteAsync<object>(method, controller, new[] { bodyParam });
            };

            return handler;
        }

        private void MapByHttpMethod(string httpMethod, string route, Delegate handler)
        {
            switch (httpMethod.ToUpper())
            {
                case "GET":
                    _app.MapGet(route, handler);
                    break;
                case "POST":
                    _app.MapPost(route, handler);
                    break;
                case "PUT":
                    _app.MapPut(route, handler);
                    break;
                case "DELETE":
                    _app.MapDelete(route, handler);
                    break;
            }
        }

        private object GetParameterValue(HttpContext context, ParameterInfo parameter)
        {
            var paramName = parameter.Name!;
            var paramType = parameter.ParameterType;

            // Buscar en route values primero
            if (context.Request.RouteValues.TryGetValue(paramName, out var routeValue))
            {
                return Convert.ChangeType(routeValue, paramType);
            }

            // Luego en query string
            if (context.Request.Query.TryGetValue(paramName, out var queryValue))
            {
                return Convert.ChangeType(queryValue.ToString(), paramType);
            }

            // Valor por defecto
            return parameter.HasDefaultValue ? parameter.DefaultValue! : Activator.CreateInstance(paramType)!;
        }

        private bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(DateTime) ||
                   type == typeof(decimal) ||
                   type == typeof(Guid) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        private Type GetActualReturnType(Type returnType)
        {
            if (returnType == typeof(Task))
                return typeof(void);

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return returnType.GetGenericArguments()[0];

            return returnType;
        }

        private string GetHttpMethod(string methodName)
        {
            if (methodName.StartsWith("Get")) return "GET";
            if (methodName.StartsWith("Create") || methodName.StartsWith("Post")) return "POST";
            if (methodName.StartsWith("Update") || methodName.StartsWith("Put")) return "PUT";
            if (methodName.StartsWith("Delete")) return "DELETE";
            return "GET";
        }

        private string GetRoute(string methodName)
        {
            if (methodName.StartsWith("Get"))
                return $"/{methodName.Substring(3).ToLower()}";
            if (methodName.StartsWith("Create"))
                return $"/{methodName.Substring(6).ToLower()}";
            if (methodName.StartsWith("Update"))
                return $"/{methodName.Substring(6).ToLower()}";
            if (methodName.StartsWith("Delete"))
                return $"/{methodName.Substring(6).ToLower()}";
            if (methodName.StartsWith("Post"))
                return $"/{methodName.Substring(4).ToLower()}";
            if (methodName.StartsWith("Put"))
                return $"/{methodName.Substring(3).ToLower()}";

            return $"/{methodName.ToLower()}";
        }
    }
}
