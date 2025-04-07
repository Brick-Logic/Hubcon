using Hubcon;
using Hubcon.Core.Models.Interfaces;

namespace HubconTestDomain
{
    public interface ITestClientController : ICommunicationContract
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
        void ShowMessage(string message);
        IAsyncEnumerable<string> GetMessages(int count);
        void ShowTextMessage(string message);
        string ShowAndReturnMessage(string message);
        string ShowAndReturnType(string message);
        void VariableParameters(string[] parameters);
        void DefaultParameters(string parameters = "parametro opcional");
        void NullableParameters(string? parameters = null);
        void TestClass(TestClass parameter);
        void NullableTestClass(TestClass? parameter);
        void DefaultNullableTestClass(TestClass? parameter = null);
        void TestClassList1(List<TestClass>? parameter = null);
        void TestClassList2(Dictionary<string, TestClass>? parameter = null);
        void TestClassList3(HashSet<TestClass>? parameter = null);
    }
}