using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface ISecondTestContract : IControllerContract
    {
        public Task<string> LoginAsync(LoginCommand command, string? url, LoginCommand command2 = null, LoginCommand command3 = null, LoginCommand command4 = null);
        public Task TestMethod();
        public Task TestVoid();
        public Task TestMethod(string message);
        public Task<string> TestReturn(string message);
        public Task<string> TestReturn();
    }
}