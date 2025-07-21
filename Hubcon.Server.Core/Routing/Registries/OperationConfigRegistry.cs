using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Routing.Registries
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OperationConfigRegistry : IOperationConfigRegistry
    {
        private readonly ConcurrentDictionary<Guid, IOperationBlueprint> _map = new();

        public bool Link(Guid observableId, IOperationBlueprint blueprint) => _map.TryAdd(observableId, blueprint);

        public bool TryGet(Guid observableId, out IOperationBlueprint blueprint) => _map.TryGetValue(observableId, out blueprint!);

        public bool Unlink(Guid observableId) => _map.TryRemove(observableId, out _);
    }
}
