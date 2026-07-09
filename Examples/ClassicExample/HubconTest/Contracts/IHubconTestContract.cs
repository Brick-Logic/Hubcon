using System.Threading.Tasks;
using Hubcon;
using HubconTestDomain;

namespace HubconTestClient.Models;

public interface IHubconTestContract : IControllerContract
{
    public Task<string> TestMethod();
}