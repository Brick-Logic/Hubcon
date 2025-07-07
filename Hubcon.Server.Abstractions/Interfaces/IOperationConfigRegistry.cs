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
        public bool Link(string observableId, IOperationBlueprint blueprint);

        public bool TryGet(string observableId, out IOperationBlueprint blueprint);

        public bool Unlink(string observableId);
    }
}
