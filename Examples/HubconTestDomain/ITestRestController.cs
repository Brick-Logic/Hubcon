using Hubcon.Core.Models.Interfaces;

namespace HubconTestDomain
{
    public interface ITestRestController : IControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
