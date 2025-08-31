using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPermissionRegistry
    {
        void Set(string tokenId, string permission, bool isAllowed, TimeSpan ttl);
        bool TryGet(string tokenId, string permission, out bool isAllowed);
    }
}
