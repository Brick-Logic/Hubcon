using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System.ComponentModel;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Routing.MethodHandling
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StreamNotificationHandler : IStreamNotificationHandler
    {
        private IDynamicConverter _converter;
        private Dictionary<string, IOnStreamReceived> StreamWaitList = new();

        public StreamNotificationHandler(IDynamicConverter converter)
        {
            _converter = converter;
        }

        public async Task<IResponse> NotifyStream(string code, ChannelReader<object> reader)
        {
            while (await reader.WaitToReadAsync())
            {
                if (reader.CanPeek)
                {
                    break;
                }
            }

            if (StreamWaitList.TryGetValue(code, out var value))
            {
                value.GetCurrentEvent()?.DynamicInvoke(reader);
            }

            return new BaseOperationResponse(true);
        }

        public Task<IAsyncEnumerable<T>> WaitStreamAsync<T>(string code)
        {
            async IAsyncEnumerable<TOut> ToAsyncEnumerable<TOut>(ChannelReader<object> reader)
            {
                await foreach (object item in reader.ReadAllAsync())
                {
                    yield return _converter.DeserializeData<TOut>(item)!;
                }
            }

            var tcs = new TaskCompletionSource<IAsyncEnumerable<T>>();
            var eventHolder = new OnStreamReceived();

            Func<ChannelReader<object>, Task> eventHandler = null!;

            eventHandler = (enumerable) =>
            {
                // Cuando el evento se dispara, resolvemos la tarea
                tcs.SetResult(ToAsyncEnumerable<T>(enumerable));
                // Es importante desuscribirse después de que el evento se ha manejado
                eventHolder.OnStreamReceivedEvent -= eventHandler;

                return Task.CompletedTask;
            };

            StreamWaitList.Add(code, eventHolder);

            eventHolder.OnStreamReceivedEvent += eventHandler;

            // Esperar a que el evento se dispare
            tcs.Task.Wait(5000);

            return tcs.Task;
        }
    }
}
