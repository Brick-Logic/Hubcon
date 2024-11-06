using Hubcon.Controllers;
using Hubcon.Models.Interfaces;

namespace TestDomain
{
    public interface ITestHubController : IClientHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
    }
}