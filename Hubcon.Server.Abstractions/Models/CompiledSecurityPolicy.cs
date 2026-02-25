using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public record CompiledSecurityPolicy(
        IReadOnlyList<IUseAuthAttribute> Handlers,
        string[] Roles,
        string[] Policies,
        bool AllowAnonymous
    );
}
