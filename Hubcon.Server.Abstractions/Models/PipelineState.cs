using Hubcon.Server.Abstractions.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Models
{
    public sealed class PipelineState
    {
        public IServiceProvider ServiceProvider = default!;
        public IOperationRequest Request = default!;
        public IOperationContext Context = default!;
        public ResultHandlerDelegate ResultHandler = default!;
        public Type[] Middlewares = default!;
        public Func<PipelineState, Task> Chain = default!;
        public int Index = 0;
    }
}
