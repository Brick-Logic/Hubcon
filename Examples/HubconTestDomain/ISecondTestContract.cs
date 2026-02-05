using Hubcon;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    [HttpTransport]
    public interface ISecondTestContract : IControllerContract
    {
        public Task<string> LoginAsync(LoginCommand command);
        public Task TestVoid();

        [HttpGet]
        public Task TestMethod(string message);

        public Task<string> TestReturn(string message);
        public Task<string> TestReturn();
    }
}