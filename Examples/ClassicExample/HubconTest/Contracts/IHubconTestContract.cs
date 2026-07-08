using System.Threading.Tasks;
using Hubcon;

namespace HubconTestClient.Models;

public interface IHubconTestContract : IControllerContract
{
    public Task<string> TestMethod();
}