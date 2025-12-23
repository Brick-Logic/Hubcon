using Hubcon.Server.Abstractions.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;

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
