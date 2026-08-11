using Hubcon.Server.Abstractions.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Models
{
    public class PipelineState
    {
        public IOperationContext Context { get; set; } = null!;
        public Type[] Middlewares { get; set; } = null!;
        public IOperationRequest Request { get; set; } = null!;
        public IServiceProvider ServiceProvider { get; set; } = null!;
        public int Index { get; set; }

        private PipelineDelegate NextDelegate { get; }

        public PipelineState()
        {
            NextDelegate = InvokeNextAsync;
        }

        public Task InvokeNextAsync()
        {
            if (Index >= Middlewares.Length)
                return Task.CompletedTask;

            var type = Middlewares[Index++];
            var middleware = (IExecutableMiddleware)ServiceProvider.GetService(type)!;

            return middleware.Execute(Request, Context, InvokeNextAsync);
        }
    }
}
