using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports.Websockets.Sessions
{
    internal abstract class IngestSession : IIngestSession
    {
        public abstract Guid Id { get; }
        public abstract void AddCancellation(Action callback, CancellationToken cancellationToken);

        public abstract void Dispose();
        public abstract void TryComplete(IngestResultMessage ingestResultMessage);
    }

    /// <summary>
    /// Represents an ingest session and manages all the resources needed.
    /// </summary>
    internal sealed class IngestSession<T> : IngestSession, IIngestSession<T>
    {
        private volatile int _started;
        private volatile int _disposed;

        private CancellationTokenRegistration? _ctr;
        private readonly TaskCompletionSource<IngestResultMessage> _tcs;
        private readonly CancellationTokenSource _cts;
        private readonly IMessageSender _sender;
        private readonly IMessageReceiver _receiver;
        private readonly IHubconWebSocket _webSocketClient;
        private readonly string _connectionId;
        private readonly IOperationRequest _operationRequest;
        private readonly IClientOptions? _clientOptions;
        private readonly IOperationOptions _operationOptions;
        private readonly List<Task> _sourceTasks;
        private readonly TaskCompletionSource<bool> _initAckTcs;
        private readonly TaskCompletionSource<IngestResultMessage> _generalTcs;
        private readonly ConcurrentDictionary<Guid, IAsyncEnumerable<JsonElement>> _sources;
        private readonly Guid _initialAckId;
        private readonly bool _loggingEnabled;
        private readonly ILogger<IngestSession<T>>? _logger;
        private readonly IDynamicConverter _converter;
        private readonly Action onCancelCallback;
        private readonly Action? _onFinishedCallback;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="webSocketClient"></param>
        /// <param name="connectionId"></param>
        /// <param name="context"></param>
        /// <param name="operationRequest"></param>
        /// <param name="operationOptions"></param>
        /// <param name="onFinishedCallback"></param>
        public IngestSession(
            IHubconWebSocket webSocketClient,
            string connectionId,
            TransportContext context,
            IOperationRequest operationRequest,
            IOperationOptions operationOptions, 
            Action? onFinishedCallback = null)
        {
            _tcs = new TaskCompletionSource<IngestResultMessage>();
            _cts = new CancellationTokenSource();
            _sender = webSocketClient.Sender;
            _receiver = webSocketClient.Receiver;
            _webSocketClient = webSocketClient;
            _connectionId = connectionId;
            _operationRequest = operationRequest;
            _clientOptions = context.ClientOptions;
            _operationOptions = operationOptions;
            _onFinishedCallback = onFinishedCallback;

            _sourceTasks = new List<Task>();
            _initAckTcs = new TaskCompletionSource<bool>();
            _generalTcs = new TaskCompletionSource<IngestResultMessage>();
            _sources = new ConcurrentDictionary<Guid, IAsyncEnumerable<JsonElement>>();
            _initialAckId = Guid.NewGuid();

            _loggingEnabled = _clientOptions?.LoggingEnabled ?? false;
            _logger = context.ProxyServiceProvider.GetService<ILogger<IngestSession<T>>>();
            _converter = context.ProxyServiceProvider.GetRequiredService<IDynamicConverter>();

            onCancelCallback = async () =>
            {        
                await _sender.SendMessageAsync(new CancelMessage(_initialAckId, _connectionId));
                _cts.Cancel();
                _generalTcs.TrySetException(new OperationCanceledException());         
            };
        }

        public override Guid Id => _initialAckId;

        /// <summary>
        /// Starts the ingest operation and gets a response. It can only be used once. It will throw an exception if already used.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="HubconGenericException"></exception>
        public async Task<T?> StartAsync(CancellationToken cancellationToken = default)
        {
            Throw.If(Interlocked.CompareExchange(ref _started, 1, 0) == 1, "'StartAsync' method from 'IngestSession' class can only be used once.");

            using var registration = cancellationToken.Register(onCancelCallback);
            Task<BaseMessage?>? receiver = null;

            try
            {
                var dict = _operationRequest.Arguments.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
                foreach (var kvp in _operationRequest.Arguments)
                {
                    if (kvp.Value != null && EnumerableTools.IsAsyncEnumerable(kvp.Value))
                    {
                        var obj = kvp.Value;
                        var id = Guid.NewGuid();
                        dict[kvp.Key] = id;
                        var stream = EnumerableTools.Wrap(obj, cancellationToken);
                        _sources.TryAdd(id, stream!);
                    }
                }

                _operationRequest.AssignArguments(dict!);

                RateLimiter? sharedLimiter = null;
                bool? useShared = null;

                if (_operationOptions != null && _operationOptions.RateBucketOptions != null)
                {
                    if (_operationOptions.RateLimiterIsShared)
                    {
                        sharedLimiter = new TokenBucketRateLimiter(_operationOptions.RateBucketOptions);
                        useShared = true;
                    }
                    else
                    {
                        useShared = false;
                    }
                }

                foreach (var source in _sources)
                {
                    var sourceTask = Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            var shouldIngest = await _initAckTcs.Task;

                            if (!shouldIngest)
                                return;

                            RateLimiter? limiter = sharedLimiter ?? (useShared == false
                                ? new TokenBucketRateLimiter(_operationOptions!.RateBucketOptions!)
                                : null);

                            await foreach (var item in source.Value.WithCancellation(cancellationToken))
                            {
                                if (_generalTcs.Task.IsCompleted || cancellationToken.IsCancellationRequested)
                                    break;

                                var message = new IngestDataMessage(source.Key, _connectionId, item);

                                try
                                {
                                    await RateLimiterHelper.AcquireAsync(_clientOptions, _clientOptions?.RateBucket, _clientOptions?.IngestRateBucket, limiter);
                                    await _sender.SendMessageAsync(message, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    if (_loggingEnabled)
                                        _logger?.LogError(ex, $"Error al enviar dato en ingest stream {source.Key}");
                                }

                                if (_generalTcs.Task.IsCompleted || cancellationToken.IsCancellationRequested)
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_loggingEnabled)
                                _logger?.LogError(ex, $"Error en ingest stream {source.Key}");
                        }
                    },
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();

                    _sourceTasks.Add(sourceTask);
                }

                var ingestRequest = new IngestInitMessage(_initialAckId, _connectionId, _sources.Keys.ToArray(), _converter.SerializeToElement(_operationRequest), default);

                try
                {
                    var ack = await _webSocketClient.SendAndReceiveAsync(ingestRequest, false, cancellationToken);

                    if (ack?.Error != null)
                    {
                        _initAckTcs.TrySetResult(false);
                        return _converter.DeserializeData<T>(ack.Error);
                    }

                    _initAckTcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    if (_loggingEnabled)
                        _logger?.LogError(ex, "Error al enviar IngestInitMessage");
                }

                _receiver.Router.BeginRequest(_initialAckId);
                receiver = _receiver.Router.GetResponseAsync(_initialAckId, TimeSpan.FromDays(23), cancellationToken);

                try
                {
                    var allIngests = Task.WhenAll(_sourceTasks);
                    var whenany = Task.WhenAny(allIngests, receiver);
                    await whenany;
                }
                finally
                {
                    registration.Dispose();
                }

                await _sender.SendMessageAsync(new IngestCompleteMessage(_initialAckId, _connectionId, _sources.Keys.ToArray()), cancellationToken);

                using BaseMessage? result = await receiver 
                    ?? await _receiver.Router.GetResponseAsync(_initialAckId, _clientOptions!.WebsocketTimeout, _cts.Token) 
                    ?? throw new HubconRemoteException("Received an empty response.");

                if (result.Error != null)
                    return _converter.DeserializeData<T>(result.Error);

                var response = _converter.DeserializeJsonElement<T>(new IngestResultMessage(result).Data) ?? throw new HubconRemoteException("Received an empty response.");

                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_loggingEnabled)
                    _logger?.LogError(ex, "Error general en IngestMultiple");

                if (HubconContext.Current.IsWrapped)
                    return default!;

                throw new HubconGenericException(ex.Message, ex);
            }
            finally
            {
                if (!_cts.IsCancellationRequested)
                {
                    var msg = new IngestCompleteMessage(_initialAckId, _connectionId, _sources.Keys.ToArray());
                    await _sender.SendMessageAsync(msg);
                }

                _onFinishedCallback?.Invoke();

                _receiver.Router.EndRequest(_initialAckId);
            }
        }

        /// <summary>
        /// Tries to complete the current stream session.
        /// </summary>
        public override void TryComplete(IngestResultMessage ingestResultMessage)
        {
            _tcs.TrySetResult(ingestResultMessage);
        }
        
        /// <summary>
        /// Allows to configure a callback when the provided cancellation token is canceled.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public override void AddCancellation(Action callback, CancellationToken cancellationToken)
        {
            _ctr ??= cancellationToken.Register(callback);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            _ctr?.Dispose();
            _tcs.TrySetCanceled();
            _cts.Cancel();
            _cts.Dispose();
            _sourceTasks.Clear();
            _initAckTcs.TrySetCanceled();
            _generalTcs.TrySetCanceled();
            _sources.Clear();

            _onFinishedCallback?.Invoke();

            GC.SuppressFinalize(this);
        }
    }
}