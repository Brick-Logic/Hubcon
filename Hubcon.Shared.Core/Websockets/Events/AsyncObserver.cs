using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Shared.Core.Websockets.Events
{
    public class AsyncObserver<T>(BoundedChannelOptions? options = null) : IObserver<T>
    {
        private readonly Channel<T?> _channel = Channel.CreateBounded<T?>(options ?? new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        private TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();

        public void WriteToChannelAsync(T? item)
        {
            if(item is JsonElement element)
                _ = _channel.Writer.TryWrite(GetTType(element));
            else
                _ = _channel.Writer.TryWrite(item);
        }

        public T GetTType(JsonElement item)
        {
            return (T)(object)item.Clone();
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
                throw;
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

        public void OnNext(T value)
        {
            // Enviar el valor al canal
            WriteToChannelAsync(value);
        }

        public Task WaitUntilCompleted()
        {
            return _completed.Task;
        }
    }
}