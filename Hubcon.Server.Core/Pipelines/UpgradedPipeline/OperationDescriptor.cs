using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    public class OperationBlueprint : IOperationBlueprint
    {
        public string OperationName { get; }
        public OperationKind Kind { get; }

        public string ContractName { get; }
        public Type ContractType { get; }

        public string ControllerName { get; }
        public Type ControllerType { get; }

        public Dictionary<string, Type> ParameterTypes { get; }
        public Type RawReturnType { get; }
        public Type ReturnType { get; }
        public bool HasReturnType { get; }

        public MemberInfo? OperationInfo { get; }

        public bool RequiresAuthorization { get; }
        public IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        public Func<object?, object[], object?>? InvokeDelegate { get; }
        public IPipelineBuilder PipelineBuilder { get; }
        public string Route { get; }


        public OperationBlueprint(
            string operationName, 
            Type contractType, 
            Type controllerType, 
            MemberInfo memberInfo, 
            IPipelineBuilder pipelineBuilder,
            IInternalServerOptions options,
            Func<object?, object[], object?>? invokeDelegate = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(operationName);
            ArgumentNullException.ThrowIfNull(contractType);
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(memberInfo);

            OperationName = operationName;
            ContractType = contractType;
            ContractName = contractType.Name;
            ControllerType = controllerType;
            ControllerName = controllerType.Name;
            OperationInfo = memberInfo;
            ParameterTypes = [];

            if (memberInfo is MethodInfo methodInfo)
            {
                foreach(var parameter in methodInfo.GetParameters())
                {
                    ParameterTypes.Add(parameter.Name!, parameter.ParameterType);
                }

                RawReturnType = methodInfo.ReturnType;

                ReturnType = methodInfo.ReturnType.IsGenericType &&
                       methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                       ? methodInfo.ReturnType.GetGenericArguments()[0]
                       : methodInfo.ReturnType;

                Route = options.HttpPathPrefix + (methodInfo.HasCustomAttribute<RouteAttribute>() 
                    ? methodInfo.GetCustomAttribute<RouteAttribute>()!.Template
                    : methodInfo.GetRoute());


                if(options.HttpPathPrefix != null)

                HasReturnType = ReturnType != typeof(void) && ReturnType != typeof(Task);

                var isStream = RawReturnType.IsGenericType 
                    && RawReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);

                Kind = isStream ? OperationKind.Stream : OperationKind.Method;
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                ReturnType = propertyInfo.PropertyType;
                RawReturnType = propertyInfo.PropertyType;
                HasReturnType = true;

                Kind = OperationKind.Subscription;
            }
            else
            {
                throw new NotSupportedException($"The type {memberInfo.GetType()} is not supported as an operation type. Use PropertyInfo o MethodInfo instead.");
            }

            RequiresAuthorization = memberInfo.HasCustomAttribute<AuthorizeAttribute>();

            AuthorizationAttributes = RequiresAuthorization == true
                ? memberInfo.GetCustomAttributes<AuthorizeAttribute>()
                : Array.Empty<AuthorizeAttribute>();

            PipelineBuilder = pipelineBuilder;
            InvokeDelegate = invokeDelegate;
        }
    }
}
