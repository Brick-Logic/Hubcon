using Hubcon;

namespace TestDomain
{
    public interface ITestHubController : IClientHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
    }
}