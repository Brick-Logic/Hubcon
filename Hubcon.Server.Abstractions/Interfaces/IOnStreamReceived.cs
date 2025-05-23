using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOnStreamReceived
    {
        public Delegate? GetCurrentEvent();
    }
}
