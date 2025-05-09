using Hubcon.Core.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Invocation
{
    public abstract class BaseResponse : IResponse
    {
        public abstract bool Success { get; }

        public abstract string? Error { get; }
    }
}
