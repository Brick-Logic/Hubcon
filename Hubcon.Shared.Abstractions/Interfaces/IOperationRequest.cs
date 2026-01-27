using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Generic;

namespace Hubcon
{
    public interface IOperationRequest : IOperationEndpoint
    {
        IReadOnlyDictionary<string, object> Arguments { get; }
        public void AssignArguments(IReadOnlyDictionary<string, object> arguments);
    }
}
