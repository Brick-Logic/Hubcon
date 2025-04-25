using Hubcon.Core.Models.Interfaces;

namespace HubconTestDomain.Interfaces
{
    public interface ISignalRServerContract : IHubconControllerContract
    {
        public Task PrintMessage(string message);
        public void VoidPrintMessage(string message);
        public Task<string> PrintMessageWithReturn(string message);
        public string VoidPrintMessageWithReturn(string message);
    }
}
