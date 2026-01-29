using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        bool ControllerExists(Type controllerType);
        bool GetOperationBlueprint(IOperationEndpoint request, HubconTransport transportAttribute, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, HubconTransport transportAttribute, out IOperationBlueprint? value);
        //void MapControllers(WebApplication app);
        void MapTransport(WebApplication app, HubconTransport transportAttribute, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? endpointRegisterer = null);
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}