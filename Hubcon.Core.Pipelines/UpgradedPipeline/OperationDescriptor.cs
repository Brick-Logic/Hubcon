using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Pipelines.UpgradedPipeline
{
    public class OperationBlueprint : IOperationBlueprint
    {
        public string OperationName { get; }
        public OperationKind Kind { get; }

        public string ContractName { get; }
        public Type ContractType { get; }

        public string ControllerName { get; }
        public Type ControllerType { get; }

        public Type[] ParameterTypes { get; }
        public Type RawReturnType { get; }
        public Type ReturnType { get; }
        public bool HasReturnType { get; }

        public MemberInfo? OperationInfo { get; }

        public bool RequiresAuthorization { get; }
        public IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        public Delegate? InvokeDelegate { get; }
        public IPipelineBuilder PipelineBuilder { get; }

        public OperationBlueprint(string operationName, Type contractType, Type controllerType, MemberInfo memberInfo, IPipelineBuilder pipelineBuilder, Delegate? invokeDelegate = null)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(operationName);
            ArgumentNullException.ThrowIfNull(contractType);
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(memberInfo);

            OperationName = operationName;
            ContractType = contractType;
            ContractName = contractType.Name;
            ControllerType = controllerType;
            ControllerName = controllerType.Name;
            OperationInfo = memberInfo;

            if (memberInfo is MethodInfo methodInfo)
            {
                ParameterTypes = methodInfo
                    .GetParameters()
                    .Select(x => x.ParameterType)
                    .ToArray() ?? Array.Empty<Type>();

                RawReturnType = methodInfo.ReturnType;

                ReturnType = methodInfo.ReturnType.IsGenericType &&
                       methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                       ? methodInfo.ReturnType.GetGenericArguments()[0]
                       : methodInfo.ReturnType;

                HasReturnType = ReturnType != typeof(void) && ReturnType != typeof(Task);

                Kind = RawReturnType.IsAssignableTo(typeof(IAsyncEnumerable<>)) ? OperationKind.Stream : OperationKind.Method;
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                ParameterTypes = Array.Empty<Type>();
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
