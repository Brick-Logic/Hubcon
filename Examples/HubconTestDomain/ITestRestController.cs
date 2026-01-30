using Hubcon;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    public interface ITestRestController : IControllerContract
    {
        Task<int> GetTemperature(string name);
    }
}
