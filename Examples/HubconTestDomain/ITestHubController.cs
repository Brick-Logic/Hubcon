using Hubcon;
using Hubcon.Core.Interfaces.Communication;

namespace HubconTestDomain
{
    public interface ITestClientController : ICommunicationContract
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
        void ShowMessage(string message);
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextMessage(string message);
        Task<string> ShowAndReturnMessage(string message);
        string ShowAndReturnType(string message);
        Task VariableParameters(string[] parameters);
        Task DefaultParameters(string parameters = "parametro opcional");
        Task NullableParameters(string? parameters = null);
        Task TestClass(TestClass parameter);
        Task NullableTestClass(TestClass? parameter);
        Task DefaultNullableTestClass(TestClass? parameter = null);
        Task TestClassList1(List<TestClass>? parameter = null);
        Task TestClassList2(Dictionary<string, TestClass>? parameter = null);
        Task TestClassList3(HashSet<TestClass>? parameter = null);
    }
}