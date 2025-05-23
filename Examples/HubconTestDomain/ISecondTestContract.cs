using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    public interface ISecondTestContract : IControllerContract
    {
        public Task<string> LoginAsync(string username, string password);
        public Task TestMethod();
        public void TestVoid();
        public Task TestMethod(string message);
        public Task<string> TestReturn(string message);
        string TestReturn();
    }
}