using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    public record LoginCommand(string Username, string Password, bool RememberMe);
}
