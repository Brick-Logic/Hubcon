using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Standard.Interceptor
{
    public abstract class BaseProxy
    {
        public abstract Task<T> InvokeAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);

        public abstract Task CallAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);

        public abstract Task<T> IngestAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);

        public abstract Task IngestAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);

        public abstract IAsyncEnumerable<T> StreamAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
    }
}