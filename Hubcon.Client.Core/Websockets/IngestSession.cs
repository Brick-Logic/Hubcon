using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Microsoft.CodeAnalysis.Operations;

namespace Hubcon.Client.Core.Websockets
{
    /// <summary>
    /// Represents an ingest session and manages all the resources needed.
    /// </summary>
    public sealed class IngestSession : IAsyncDisposable
    {
        private readonly TaskCompletionSource<IngestResultMessage> _tcs;
        private readonly CancellationTokenSource _cts;
        private CancellationTokenRegistration? _ctr;

        public IngestSession()
        {
            _tcs = new TaskCompletionSource<IngestResultMessage>();
            _cts = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Tries to complete the current stream session.
        /// </summary>
        public void TryComplete(IngestResultMessage ingestResultMessage)
        {
            _tcs.TrySetResult(ingestResultMessage);
        }
        
        /// <summary>
        /// Allows to configure a callback when the provided cancellation token is canceled.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public void AddCancellation(Action callback, CancellationToken cancellationToken)
        {
            _ctr ??= cancellationToken.Register(callback);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            
        }
    }
}