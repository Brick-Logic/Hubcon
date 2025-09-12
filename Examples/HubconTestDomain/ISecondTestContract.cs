using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface ISecondTestContract : IControllerContract
    {
        [GetMethod]
        public Task<string> LoginAsync(string username, string password);
        public Task TestMethod();
        public Task TestVoid();
        public Task TestMethod(string message);
        public Task<string> TestReturn(string message);
        public Task<string> TestReturn();
    }
}