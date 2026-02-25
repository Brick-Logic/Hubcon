using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        bool ControllerExists(Type controllerType);
        bool TryGetOperationBlueprint(IOperationEndpoint request, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value);
        //void MapControllers(WebApplication app);
        void MapTransport<T>(WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? endpointRegisterer = null) where T : HubconTransportAttribute, new();
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}