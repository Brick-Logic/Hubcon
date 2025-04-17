using Hubcon.SignalR.Client;
using HubconTestDomain;
using System;

namespace HubconTestClient
{
    public class TestHubController : BaseSignalRClientController<IServerHubContract>, ITestClientController
    {
        // Función simple que vamos a apuntar

        //unsafe struct FixedBuffer
        //{
        //    public fixed int numeros[10];
        //}

        public TestHubController()
        {

            //Span<byte> bytes = stackalloc byte[100];
            //nint ptr = (nint)0x12345678;

            //unsafe
            //{
            //    int x = 42;
            //    int* p = &x;
            //    Console.WriteLine(*p);

            //    static int Sumar(int a, int b) => a + b;        
            //    delegate*<int, int, int> sumaPtr = &Sumar;
            //    Delegate suma = Sumar;


            //}
        }




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
                yield return "hola";
            }
        }

        public string ShowAndReturnMessage(string message)
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

        public void ShowTextMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void VariableParameters(params string[] parameters)
        {
            Console.WriteLine($"Variable parameters: {parameters.Length} received");
        }

        public void DefaultParameters(string parameters = "parametro opcional")
        {
            Console.WriteLine(parameters);
        }

        public void NullableParameters(string? parameters = null)
        {
            Console.WriteLine($"Nullable reached: {parameters == null}");
        }


        public void TestClass(TestClass parameter)
        {
            Console.WriteLine($"TestClass reached: {parameter.Id}, {parameter.Name}");
        }

        public void NullableTestClass(TestClass? parameter)
        {
            Console.WriteLine($"NullableTestClass reached: {parameter?.Id}, {parameter?.Name}");
        }

        public void DefaultNullableTestClass(TestClass? parameter = null)
        {
            Console.WriteLine($"DefaultNullableTestClass reached: {parameter == null}");
        }

        public void TestClassList1(List<TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList1 reached: {parameter == null}, {parameter?.Count}");
        }

        public void TestClassList2(Dictionary<string, TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList2 reached: {parameter == null}, {parameter?.Count}");
        }

        public void TestClassList3(HashSet<TestClass>? parameter = null)
        {
            Console.WriteLine($"TestClassList3 reached: {parameter == null}, {parameter?.Count}");
        }
    }
}
