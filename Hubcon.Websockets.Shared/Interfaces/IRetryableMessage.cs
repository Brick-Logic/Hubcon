using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Interfaces
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



