using Autofac;
using Castle.Core.Internal;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Data;
using Hubcon.Server.Models;
using Hubcon.Server.Models.CustomAttributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Server.Injection
{
    public class GraphQLControllerOptions : IControllerOptions
    {
        public GraphQLControllerOptions(IRequestExecutorBuilder executorBuilder, WebApplicationBuilder builder, HubconServerBuilder hubconBuilder)
        {
            ExecutorBuilder = executorBuilder;
            Builder = builder;
            HubconServerBuilder = hubconBuilder;
        }

        public IRequestExecutorBuilder ExecutorBuilder { get; }
        public WebApplicationBuilder Builder { get; }
        public HubconServerBuilder HubconServerBuilder { get; }

        public void SetEntrypoint(Type controllerType)
        {
            if (!controllerType.IsAssignableTo(typeof(IHubconEntrypoint)))
                throw new ArgumentException($"El tipo {controllerType.Name} no implementa la interfaz {nameof(IHubconEntrypoint)}");

            var methods = controllerType
                .GetMethods()
                .Where(method => method.IsDefined(typeof(HubconMethodAttribute), inherit: false));

            var mutations = methods.Where(method => method.GetAttribute<HubconMethodAttribute>().MethodType == MethodType.Mutation);
            var subscriptions = methods.Where(method => method.GetAttribute<HubconMethodAttribute>().MethodType == MethodType.Subscription);

            ExecutorBuilder.AddMutationType(descriptor =>
            {
                RegisterMethods(controllerType, descriptor, mutations);
            });
            
            ExecutorBuilder.AddSubscriptionType(descriptor => RegisterSubscriptions(controllerType, descriptor, subscriptions));
        }

        public void SetEntrypoint<TIHubconEntrypoint>()
            where TIHubconEntrypoint : class, IHubconEntrypoint
                => SetEntrypoint(typeof(TIHubconEntrypoint));

        private void RegisterMethods<TIHubconEntrypoint>(IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
            where TIHubconEntrypoint : class, IHubconEntrypoint
                => RegisterMethods(typeof(TIHubconEntrypoint), descriptor, methods);
        private void RegisterMethods(Type controllerType, IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
        {
            if (!controllerType.IsAssignableTo(typeof(IHubconEntrypoint)))
                throw new ArgumentException($"El tipo {controllerType.Name} no implementa la interfaz {nameof(IHubconEntrypoint)}");

            foreach (var method in methods)
            {
                var fieldDescriptor = descriptor.Field(method.Name!) // Usa el nombre real del método
                    .Resolve(async context =>
                    {
                        // Instancia del controlador
                        var controller = (IHubconEntrypoint)context.Service<ILifetimeScope>().Resolve(controllerType);

                        // Prepara argumentos desde el contexto
                        var args = method.GetParameters().Select(p =>
                            context.ArgumentValue<object>(p.Name!)
                        ).ToArray();

                        // Llama al método
                        var result = method.Invoke(controller, args);

                        // Espera si es Task
                        if (result is Task taskResult)
                        {
                            await taskResult;
                            return taskResult.GetType().IsGenericType
                                ? ((dynamic)taskResult).Result
                                : null;
                        }

                        return result;
                    });

                foreach (var parameter in method.GetParameters())
                {
                    fieldDescriptor.Argument(parameter.Name!, x => x.Type(parameter.ParameterType));
                }

                // Resolver tipo de retorno
                var returnType = UnwrapReturnType(method.ReturnType);
                var gqlOutputType = GetGraphQLOutputType(returnType);
                if (gqlOutputType != null)
                    fieldDescriptor.Type(gqlOutputType);
            }
        }

        private void RegisterSubscriptions<TIHubconEntrypoint>(IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
            where TIHubconEntrypoint : class, IHubconEntrypoint
                => RegisterSubscriptions(typeof(TIHubconEntrypoint), descriptor, methods);
        private void RegisterSubscriptions(Type controllerType, IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
        {
            if (!controllerType.IsAssignableTo(typeof(IHubconEntrypoint)))
                throw new ArgumentException($"El tipo {controllerType.Name} no implementa la interfaz {nameof(IHubconEntrypoint)}");

            foreach (var method in methods)
            {
                var fieldDescriptor = descriptor.Field(method.Name!)
                    .Type<JsonScalarType>()
                    .Subscribe(context =>
                    {
                        var controller = (IHubconEntrypoint)context.Service<ILifetimeScope>().Resolve(controllerType);

                        controller.Build();

                        var args = method.GetParameters().Select(p =>
                            context.ArgumentValue<object>(p.Name!)
                        ).ToArray();

                        var result = method.Invoke(controller, args);

                        if (result is IAsyncEnumerable<JsonElement> asyncStream)
                            return asyncStream;

                        return (IAsyncEnumerable<JsonElement>)result!;
                    })
                    .Resolve(ctx =>
                    {
                        var message = ctx.ScopedContextData["HotChocolate.Execution.EventMessage"];
                        if (message is JsonElement json)
                            return json.GetRawText();

                        return "null";
                    });

                foreach (var parameter in method.GetParameters())
                {
                    fieldDescriptor.Argument(parameter.Name!, x => x.Type(parameter.ParameterType));
                }

                var returnType = UnwrapReturnType(method.ReturnType);
                var gqlOutputType = GetGraphQLOutputType(returnType);

                if (gqlOutputType != null)
                    fieldDescriptor.Type(gqlOutputType);
            }
        }

        private static Type UnwrapReturnType(Type returnType)
        {
            if (typeof(Task).IsAssignableFrom(returnType))
            {
                if (returnType.IsGenericType)
                    return returnType.GetGenericArguments()[0];
                return typeof(void);
            }

            return returnType;
        }

        private static ITypeNode? GetGraphQLOutputType(Type type)
        {
            if (type == typeof(void))
                return null;
            if (type == typeof(int))
                return new NamedTypeNode("Int");
            if (type == typeof(string))
                return new NamedTypeNode("String");
            if (type == typeof(JsonElement) || type == typeof(JsonElement))
                return new NamedTypeNode("JsonScalarType");
            if (type == typeof(BaseJsonResponse))
                return new NamedTypeNode("BaseJsonResponse");
            if (type == typeof(IResponse))
                return new NamedTypeNode("IResponse");
            if (type == typeof(IOperationResponse<JsonElement>))
                return new NamedTypeNode("BaseJsonResponse");
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var inner = GetGraphQLOutputType(type.GetGenericArguments()[0]);
                return new ListTypeNode(inner!);
            }

            throw new NotSupportedException($"No se puede mapear el tipo de salida {type.Name}");
        }
        
        private static ITypeNode GetGraphQLInputType(Type type)
        {
            if (type == typeof(string))
                return new NamedTypeNode("String");
            if (type == typeof(int))
                return new NamedTypeNode("Int");
            if (type == typeof(JsonElement) || type == typeof(JsonElement))
                return new NamedTypeNode("JsonScalarType");

            throw new NotSupportedException($"No se puede mapear el tipo de entrada {type.Name}");
        }

        public void AddGlobalMiddleware<T>()
        {
            HubconServerBuilder.AddGlobalMiddleware<T>();
        }

        public void AddGlobalMiddleware(Type middlewareType)
        {
            HubconServerBuilder.AddGlobalMiddleware(middlewareType);
        }

        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract
        {
            HubconServerBuilder.AddHubconController<T>(Builder, options);
        }

        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null)
        {
            HubconServerBuilder.AddHubconController(Builder, controllerType, options);
        }
    }
}
