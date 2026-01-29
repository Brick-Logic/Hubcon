using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Configuration
{
    public sealed class SettingsManager(IOperationRegistry operationRegistry, IOperationConfigRegistry operationConfigRegistry) : ISettingsManager
    {
        public T GetSettings<T>(IOperationEndpoint operationRequest, HubconTransport transportAttribute, Func<T> onNull)
        {
            if (!operationRegistry.GetOperationBlueprint(operationRequest, transportAttribute, out var blueprint))
                return onNull.Invoke();

            if (blueprint!.ConfigurationAttributes.TryGetValue(typeof(T), out Attribute? value)
                && value is T settings)
            {
                return settings;
            }

            return onNull.Invoke();
        }

        public T GetSettings<T>(Guid linkId, Func<T> onNull)
        {
            if (operationConfigRegistry.TryGet(linkId, out var blueprint)
                && blueprint.ConfigurationAttributes.TryGetValue(typeof(T), out Attribute? value)
                && (value is T settings))
            {
                return settings;
            }

            return onNull.Invoke();
        }
    }
}
