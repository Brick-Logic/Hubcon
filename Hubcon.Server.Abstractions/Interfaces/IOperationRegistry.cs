using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        void Build(ITransportAttribute transport);
        bool ControllerExists(Type controllerType);
        bool GetOperationBlueprint(IOperationEndpoint request, ITransportAttribute transportAttribute, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, ITransportAttribute transportAttribute, out IOperationBlueprint? value);
        //void MapControllers(WebApplication app);
        void MapTransport(WebApplication app, ITransportAttribute transportAttribute, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication> endpointRegisterer);
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}