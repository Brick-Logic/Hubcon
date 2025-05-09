using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface ITestRestController : IControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
