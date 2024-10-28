using Hubcon.Controller;

namespace TestDomain
{
    public interface ITestHubController : IHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
    }
}
