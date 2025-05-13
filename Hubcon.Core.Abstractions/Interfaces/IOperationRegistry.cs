using Autofac;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        bool GetOperationDescriptor(IOperationRequest request, out IOperationBlueprint? value);
        void RegisterOperations(Type controllerType, Action<IMiddlewareOptions>? options, out List<Action<ContainerBuilder>> servicesToInject);
    }
}