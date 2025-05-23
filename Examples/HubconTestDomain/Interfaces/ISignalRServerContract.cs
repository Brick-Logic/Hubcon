using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain.Interfaces
{
    public interface ISignalRServerContract : IControllerContract
    {
        public Task PrintMessage(string message);
        public void VoidPrintMessage(string message);
        public Task<string> PrintMessageWithReturn(string message);
        public string VoidPrintMessageWithReturn(string message);
    }
}
