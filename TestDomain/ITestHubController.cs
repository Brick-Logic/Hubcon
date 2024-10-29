using Hubcon.Controllers;
using Hubcon.Models;

namespace TestDomain
{
    public interface ITestHubController : IHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
    }
}