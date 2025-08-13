using Hubcon.Shared.Abstractions.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;

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
    public sealed class ChannelAsyncObserver<T>(IDynamicConverter converter, BoundedChannelOptions? options = null) : IAsyncObserver<T>, IObserver<T>
    {
        private readonly Channel<T?> _channel = Channel.CreateBounded<T?>(options ?? new BoundedChannelOptions(5000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        private TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();

        public async Task<bool> WriteToChannelAsync(T? item)
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
            catch(Exception ex)
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

        public IAsyncEnumerable<T?> GetAsyncEnumerable(CancellationToken cancellationToken)
        {
            try
            {
                return ReadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                OnError(ex);
                return default!;
            }
        }

        private async IAsyncEnumerable<T?> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return item;
                }
            }
            finally
            {
            }
        }

        public async Task<T?> ReadItemAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _channel.Reader.ReadAsync(cancellationToken);
                return result;
            }
            catch (ChannelClosedException)
            {
                return default;
            }
        }

        public void OnCompleted()
        {
            _channel.Writer.Complete();
            _completed.SetResult(true);
        }

        public void OnError(Exception error)
        {
            _channel.Writer.Complete(error);
            _completed.SetException(error);
        }

        public async void OnNext(T value)
        {
            await WriteToChannelAsync(value);
        }

        public Task WaitUntilCompleted()
        {
            return _completed.Task;
        }
    }
}