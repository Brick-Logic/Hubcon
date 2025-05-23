using Autofac;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        bool GetOperationBlueprint(IOperationRequest request, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, out IOperationBlueprint? value);
        void RegisterOperations(Type controllerType, Action<IMiddlewareOptions>? options, out List<Action<ContainerBuilder>> servicesToInject);
    }
}