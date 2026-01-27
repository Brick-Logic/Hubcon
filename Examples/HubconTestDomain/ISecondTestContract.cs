using Hubcon;

namespace HubconTestDomain
{
    public interface ISecondTestContract : IControllerContract
    {
        public Task<string> LoginAsync(LoginCommand command);
        public Task TestMethod();
        public Task TestVoid();

        [HttpGet]
        public Task TestMethod(string message);
        public Task<string> TestReturn(string message);
        public Task<string> TestReturn();
    }
}