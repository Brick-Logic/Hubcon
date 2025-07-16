using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Routing.Registries
{
    public class OperationConfigRegistry : IOperationConfigRegistry
    {
        private readonly ConcurrentDictionary<string, IOperationBlueprint> _map = new();

        public bool Link(string observableId, IOperationBlueprint blueprint) => _map.TryAdd(observableId, blueprint);

        public bool TryGet(string observableId, out IOperationBlueprint blueprint) => _map.TryGetValue(observableId, out blueprint!);

        public bool Unlink(string observableId) => _map.TryRemove(observableId, out _);
    }
}
