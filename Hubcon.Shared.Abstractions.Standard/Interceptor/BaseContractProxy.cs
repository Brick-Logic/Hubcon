using Hubcon.Shared.Abstractions.Standard.Cache;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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