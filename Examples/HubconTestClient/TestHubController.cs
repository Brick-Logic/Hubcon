using Hubcon.SignalR.Client;
using HubconTestDomain;

namespace HubconTestClient
{
    public class TestHubController(string url) : BaseSignalRClientController<IServerHubContract>(url), ITestClientController
    {
        public async Task ShowText() => await Task.Run(() => Console.WriteLine("ShowText() invoked succesfully."));
        public async Task<int> GetTemperature() => await Task.Run(() => new Random().Next(-10, 50));

        public async Task Random()
        {
            var temperatura = await Server.GetTemperatureFromServer();
            Console.WriteLine($"Temperatura desde el conector: {temperatura}");
        }

        public async IAsyncEnumerable<string> GetMessages(int count)
        {
            for(int i = 0; i < count; i++)
            {
                await Task.Delay(100);
                yield return "hola";
            }
        }

        public async Task<string> ShowAndReturnMessage(string message)
        {
            string returnMessage = "Hola desde el controller del cliente.";
            Console.WriteLine($"Cliente: Metodo {nameof(ShowAndReturnMessage)} llamado. Mensaje recibido: {message}. Respondiendo con '{returnMessage}'");
            return returnMessage;
        }

        public string ShowAndReturnType(string message)
        {
            Console.WriteLine(message);
            return message;
        }

        public void ShowMessage(string message) => Console.WriteLine(message);

        public async Task ShowTextMessage(string message)
        {
            Console.WriteLine(message);
        }

        public async Task VariableParameters(params string[] parameters)
        {
            Console.WriteLine($"Variable parameters: {parameters.Length} received");
        }

        public async Task DefaultParameters(string parameters = "parametro opcional")
        {
            Console.WriteLine(parameters);
        }

        public async Task NullableParameters(string? parameters = null)
        {
            Console.WriteLine($"Nullable reached: {parameters == null}");
        }


        public async Task TestClass(TestClass parameter)
        {
            Console.WriteLine($"TestClass reached: {parameter.Id}, {parameter.Name}");
        }

        public async Task NullableTestClass(TestClass? parameter)
        {
            Console.WriteLine($"NullableTestClass reached: {parameter?.Id}, {parameter?.Name}");
        }

        public async Task DefaultNullableTestClass(TestClass? parameter = null)
        {
            Console.WriteLine($"DefaultNullableTestClass reached: {parameter == null}");
        }

        public async Task TestClassList1(List<TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList1 reached: {parameter == null}, {parameter?.Count}");
        }

        public async Task TestClassList2(Dictionary<string, TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList2 reached: {parameter == null}, {parameter?.Count}");
        }

        public async Task TestClassList3(HashSet<TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList3 reached: {parameter == null}, {parameter?.Count}");
        }
    }
}
