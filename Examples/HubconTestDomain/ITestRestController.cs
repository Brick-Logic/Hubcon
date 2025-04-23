using Hubcon.Core.Models.Interfaces;

namespace HubconTestDomain
{
    public interface ITestRestController : ICommunicationContract
    {
        Task<int> GetTemperature(string name);
    }
}
