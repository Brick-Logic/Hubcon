using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationRegistry
    {
        event Action<IOperationBlueprint>? OnOperationRegistered;

        void Build();
        bool ControllerExists(Type controllerType);
        bool GetOperationBlueprint(IOperationEndpoint request, out IOperationBlueprint? value);
        bool GetOperationBlueprint(string contractName, string operationName, out IOperationBlueprint? value);
        void MapControllers(WebApplication app);
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}