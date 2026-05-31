using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IIngestSession : IAsyncDisposable
    {
        Guid Id { get; }

        void AddCancellation(Action callback, CancellationToken cancellationToken);
        void TryComplete(IngestResultMessage ingestResultMessage);
    }

    public interface IIngestSession<T> : IIngestSession
    {
        Task<T?> StartAsync(CancellationToken cancellationToken = default);
    }
}
