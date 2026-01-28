using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        void Build(TransportAttribute transport);
        bool ControllerExists(Type controllerType);
        bool GetOperationBlueprint(IOperationEndpoint request, TransportAttribute transportAttribute, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, TransportAttribute transportAttribute, out IOperationBlueprint? value);
        //void MapControllers(WebApplication app);
        void MapTransport(WebApplication app, TransportAttribute transportAttribute, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication> endpointRegisterer);
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}