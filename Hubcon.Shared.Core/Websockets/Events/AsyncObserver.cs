#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Events
{
    public static class AsyncObserver
    {
        public static IAsyncObserver<T> Create<T>(IDynamicConverter converter, BoundedChannelOptions? options = null)
        {
            return new ChannelAsyncObserver<T>(converter, options);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ChannelAsyncObserver<T> : IAsyncObserver<T>, IObserver<T>
    {
        private readonly Channel<T> _channel;
        private readonly IDynamicConverter converter;
        private TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();
        public event Action? Completed;
        public event Action? Error;
        public event Action<T>? Next;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ChannelAsyncObserver(IDynamicConverter converter, BoundedChannelOptions? options = null)
        {
            this.converter = converter;
            _channel = Channel.CreateBounded<T>(options ?? new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        }

        public async Task<bool> WriteToChannelAsync(T item)
        {
            try
            {
                var toWrite = item is JsonElement element
                    ? GetTType(element)
                    : item;

                await _channel.Writer.WriteAsync(toWrite!);
                return true;
            }
            catch (ChannelClosedException ex)
            {
                // El canal ya fue completado/cerrado
                return false;
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        private T GetTType(JsonElement item)
        {
            // Si T es JsonElement, devolver directamente sin Clone()
            if (typeof(T) == typeof(JsonElement))
                return (T)(object)item;

            // Para otros tipos, deserializar desde JSON string
            return converter.DeserializeData<T>(item)!;
        }

        public IAsyncEnumerable<T> GetAsyncEnumerable(Action? disposeAction = null)
        {
            try
            {
                return ReadAsync(disposeAction);
            }
            catch (Exception ex)
            {
                OnError(ex);
                return default!;
            }
        }

        private async IAsyncEnumerable<T> ReadAsync(Action? disposeAction = null)
        {
            await foreach (var item in _channel.Reader.ReadAllAsync())
            {
                yield return item;
            }

            disposeAction?.Invoke();
        }

        public async Task<T> ReadItemAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _channel.Reader.ReadAsync(cancellationToken);
                return result;
            }
            catch (ChannelClosedException)
            {
                return default!;
            }
        }

        public void OnCompleted()
        {
            try
            {
                var completed = _channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
            }

            _completed.TrySetResult(true);
            Completed?.Invoke();
        }

        public void OnError(Exception error)
        {
            try
            {
                var completed = _channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
            }

            _completed.TrySetException(error);
            Error?.Invoke();
        }

        public async void OnNext(T value)
        {
            _gate.Wait();

            try
            {
                await WriteToChannelAsync(value);
                Next?.Invoke(value);
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task WaitUntilCompleted()
        {
            return _completed.Task;
        }
    }
}