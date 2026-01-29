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
        void MapTransport<T>(WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? endpointRegisterer = null) where T : HubconTransport, new();
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}