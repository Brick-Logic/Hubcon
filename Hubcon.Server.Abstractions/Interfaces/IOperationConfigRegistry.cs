using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOperationConfigRegistry
    {
        public bool Link(Guid observableId, IOperationBlueprint blueprint);

        public bool TryGet(Guid observableId, out IOperationBlueprint blueprint);

        public bool Unlink(Guid observableId);
    }
}
