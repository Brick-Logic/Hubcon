using Autofac;
using Castle.Core.Internal;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using Hubcon.Core;
using Hubcon.Core.Controllers;
using Hubcon.Core.Dummy;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.CustomAttributes;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconGraphQL(
            this WebApplicationBuilder builder,
            Action<IRequestExecutorBuilder>? controllerOptions = null,
            Action<ContainerBuilder>? additionalServices = null)
        {

            var executorBuilder = builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddType<BaseResponse>()
                .AddType<BaseMethodResponse>()
                .AddType<BaseJsonResponse>()
                .AddType<JsonScalarType>()
                .AddType<ObjectType<IResponse>>()    // Registra tipos como ObjectType si es necesario
                .AddType<ObjectType<IMethodResponse<JsonElement?>>>()    // Registra tipos como ObjectType si es necesario
                .AddType<InputObjectType<MethodInvokeRequest>>()  // Para cualquier tipo de entrada
                .AddProjections();

            controllerOptions?.Invoke(executorBuilder);
      
            builder.AddHubcon(additionalServices, container =>
            {
                if (ControllerTypes != null && ControllerTypes.Count > 0)
                    foreach (var controller in ControllerTypes)
                        container.RegisterWithInjector(x => x.RegisterType(controller));

                ControllerTypes?.Clear();

                container.RegisterWithInjector(x => x
                    .RegisterType(typeof(HubconControllerManager<DummyCommunicationHandler>))
                    .As<IHubconControllerManager>()
                    .AsScoped());
            });

            return builder;
        }

        private static List<Type> ControllerTypes { get; } = new();

        public static IRequestExecutorBuilder AddController<TController>(this IRequestExecutorBuilder e)
            where TController : class, IHubconServerController
        {
            ControllerTypes.Add(typeof(TController));
            var controllerType = typeof(TController);
            var methods = controllerType
                .GetMethods()
                .Where(method => method.IsDefined(typeof(HubconMethodAttribute), inherit: false));

            var mutations = methods.Where(method => method.GetAttribute<HubconMethodAttribute>().MethodType == MethodType.Mutation);
            var subscriptions = methods.Where(method => method.GetAttribute<HubconMethodAttribute>().MethodType == MethodType.Subscription);

            if(mutations.Any())
                e.AddMutationType(descriptor =>
                {
                    RegisterMethods<TController>(descriptor, mutations);
                });

            if (subscriptions.Any())
                e.AddSubscriptionType(descriptor =>
                {
                    RegisterSubscriptions<TController>(descriptor, subscriptions);
                });

            return e;
        }

        private static void RegisterMethods<TController>(IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
            where TController : class, IHubconServerController
        {
            foreach (var method in methods)
            {
                var fieldDescriptor = descriptor.Field(method.Name!) // Usa el nombre real del método
                    .Resolve(async context =>
                    {
                        // Instancia del controlador
                        var controller = context
                        .Service<ILifetimeScope>()
                                .Resolve<TController>();

                        controller.Build();

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

        private static void RegisterSubscriptions<TController>(IObjectTypeDescriptor descriptor, IEnumerable<MethodInfo> methods)
            where TController : class, IHubconServerController
        {
            foreach (var method in methods)
            {
                var fieldDescriptor = descriptor.Field(method.Name!) // Usa el nombre real del método
                    .Type<JsonScalarType>()
                    .Subscribe(context =>
                    {
                        // Instancia del controlador
                        var controller = context
                            .Service<ILifetimeScope>()
                            .Resolve<TController>();

                        controller.Build();

                        // Prepara argumentos desde el contexto
                        var args = method.GetParameters().Select(p =>
                            context.ArgumentValue<object>(p.Name!)
                        ).ToArray();

                        // Llama al método
                        var result = method.Invoke(controller, args);

                        if (result is IAsyncEnumerable<JsonElement?> asyncStream)
                            return asyncStream;

                        return (IAsyncEnumerable<JsonElement?>)result!;
                    })
                    .Resolve(ctx =>
                    {
                        var message = ctx.ScopedContextData["HotChocolate.Execution.EventMessage"];
                        if (message is JsonElement json)
                            return json.GetRawText();

                        return "null";
                    });

                // Agrega los argumentos como siempre
                foreach (var parameter in method.GetParameters())
                {
                    fieldDescriptor.Argument(parameter.Name!, x => x.Type(parameter.ParameterType));
                }

                // Resolver tipo de retorno
                var returnType = UnwrapReturnType(method.ReturnType); // ya deberías tener esto implementado
                var gqlOutputType = GetGraphQLOutputType(returnType); // lo mismo

                if (gqlOutputType != null)
                    fieldDescriptor.Type(gqlOutputType);
            }
        }


        public static WebApplication MapHubconGraphQL(this WebApplication builder, string path)
        {
            builder.MapGraphQL(path);

            return builder;
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
            if (type == typeof(JsonElement) || type == typeof(JsonElement?))
                return new NamedTypeNode("JsonScalarType"); // necesitás un tipo escalar personalizado para esto
            if (type == typeof(BaseJsonResponse))
                return new NamedTypeNode("BaseJsonResponse");
            if (type == typeof(IResponse))
                return new NamedTypeNode("IResponse"); // asumimos que está registrado como ObjectType<IResponse>
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
            if (type == typeof(JsonElement) || type == typeof(JsonElement?))
                return new NamedTypeNode("JsonScalarType"); // también necesitás escalar personalizado acá
            if (type == typeof(MethodInvokeRequest))
                return new NamedTypeNode("MethodInvokeRequest");

            throw new NotSupportedException($"No se puede mapear el tipo de entrada {type.Name}");
        }
    }
}