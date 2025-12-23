using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Interfaces
{
    public interface IRetryableMessage
    {
        public int RetryCount { get; }
        public Task AckAsync();
        public Task FailedAckAsync();
        Task<bool> CanRetry();
        void GetPayload(out object? message);
    }
}



