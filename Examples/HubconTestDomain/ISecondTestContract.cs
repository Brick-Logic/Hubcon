using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hubcon;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    [HttpTransport]
    public interface ISecondTestContract : IControllerContract
    {
        public Task<LoginResponse> LoginAsync(LoginCommand loginCommand, string id);
        
        public Task TestVoid();

        [HttpGet]
        public Task TestMethod([Required] string message);

        public Task<string> TestReturnWithParameter(string message);
        public Task<string> TestReturn();
        Task<HubconResponse<bool>> TestHubconResponse();
    }
}