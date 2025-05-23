using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface ITestRestController : IControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
