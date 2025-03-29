using Hubcon.Core.Interfaces.Communication;

namespace Hubcon.SignalR.Models.Interfaces
{
    public interface ISignalRServerContract : ICommunicationContract
    {
        public Task PrintMessage(string message);
        public void VoidPrintMessage(string message);
        public Task<string> PrintMessageWithReturn(string message);
        public string VoidPrintMessageWithReturn(string message);
    }
}
