using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Interfaces
{
    public interface IUnsubscriber
    {
        Task Unsubscribe(IRequest request);
    }
}
