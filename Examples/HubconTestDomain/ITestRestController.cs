using Hubcon;

namespace HubconTestDomain
{
    public interface ITestRestController : IControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
