using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Configuration
{
    public sealed class SettingsManager(IOperationRegistry operationRegistry, IOperationConfigRegistry operationConfigRegistry) : ISettingsManager
    {
        public T GetSettings<T>(IOperationEndpoint operationRequest, Func<T> onNull)
        {
            if (!operationRegistry.GetOperationBlueprint(operationRequest, out var blueprint))
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
