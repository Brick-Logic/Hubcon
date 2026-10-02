using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconStream
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    }
}