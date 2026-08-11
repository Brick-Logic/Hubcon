using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Abstractions.Models;
using Hubcon;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    internal class PipelineExecutor(PipelineState state) : IPipelineExecutor
    {
        public async ValueTask<IOperationContext> Execute()
        {
            try
            {
                OperationContextProvider.SetContext(state.Context);
                await state.InvokeNextAsync();
                return state.Context;
            }
            finally
            {
                OperationContextProvider.ClearContext();
            }
        }
    }
}
