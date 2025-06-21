using Autofac;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        bool GetOperationBlueprint(IOperationRequest request, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, out IOperationBlueprint? value);
        void MapControllers(WebApplication app);
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, out List<Action<ContainerBuilder>> servicesToInject);
    }
}