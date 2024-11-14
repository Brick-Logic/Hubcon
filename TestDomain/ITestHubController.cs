using Hubcon;

namespace TestDomain
{
    public interface ITestClientController : IClientController
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
        void ShowMessage(string message);
        Task ShowTextMessage(string message);
        Task<string> ShowAndReturnMessage(string message);
        string ShowAndReturnType(string message);
        Task VariableParameters(string[] parameters);
        Task DefaultParameters(string parameters = "parametro opcional");
        Task NullableParameters(string? parameters = null);
        public Task TestClass(TestClass parameter);
        public Task NullableTestClass(TestClass? parameter);
        public Task DefaultNullableTestClass(TestClass? parameter = null);
        public Task TestClassList1(List<TestClass>? parameter = null);
        public Task TestClassList2(Dictionary<string, TestClass>? parameter = null);
        public Task TestClassList3(HashSet<TestClass>? parameter = null);
    }
}