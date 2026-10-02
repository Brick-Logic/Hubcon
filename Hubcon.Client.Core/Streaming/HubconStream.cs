using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hubcon;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Tools;

namespace Hubcon
{
    public class HubconStream<T, TState> : IAsyncEnumerable<T>, IHubconStream
    {
        private readonly Func<TState, CancellationToken, ValueTask<IAsyncEnumerable<T>>> _getStream;
        private readonly TState _state;
        private IAsyncEnumerable<T> _stream = null!;
        private readonly AtomicPass _isInitializedPass = new();

        public HubconStream(Func<TState, CancellationToken, ValueTask<IAsyncEnumerable<T>>> getStream, TState state)
        {
            _getStream = getStream;
            _state = state;
        }

        public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitializedPass.TryAcquirePass()) return;
            _stream = await _getStream.Invoke(_state, cancellationToken);
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (!_isInitializedPass.WasAcquired)
            {
                await InitializeAsync(cancellationToken);
            }

            await foreach (var item in _stream.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }
}