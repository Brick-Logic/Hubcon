using Hubcon.SignalR.Server;
using HubconTestDomain;

namespace HubconTest.Controllers
{
    public class TestSignalRController : BaseHubController<ITestClientController>, IServerHubContract
    {
        public async Task<int> GetTemperatureFromServer() => await Task.Run(() => new Random().Next(-10, 50));

        public async Task PrintMessage(string message)
        {
            Console.WriteLine($"[Servidor] Mensaje recibido: {message}");
            string message2 = "PONG";
            Console.WriteLine($"[Servidor] Devolviendo mensaje al cliente: {message2}");

            CurrentClient?.ShowMessage(message2);
        }

        public Task ShowTempOnServerFromClient()
        {
            throw new NotImplementedException();
        }

        public Task ShowTextOnServer()
        {
            throw new NotImplementedException();
        }
    }
}
