#pragma warning disable CS1591
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Interfaces
{
    public interface IAwaitableAckMessage
    {
        Task<bool> WaitAckAsync();
    }
}