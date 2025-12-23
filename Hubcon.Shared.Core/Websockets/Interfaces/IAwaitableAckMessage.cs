using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Interfaces
{
    public interface IAwaitableAckMessage
    {
        Task<bool> WaitAckAsync();
    }
}