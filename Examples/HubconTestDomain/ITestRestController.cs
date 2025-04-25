using Hubcon.Core.Models.Interfaces;

namespace HubconTestDomain
{
    public interface ITestRestController : IHubconControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
