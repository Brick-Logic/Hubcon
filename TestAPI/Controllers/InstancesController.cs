using Hubcon;
using Microsoft.AspNetCore.Mvc;
using TestAPI.HubControllers;
using TestDomain;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstancesController(ClientConnector<ITestClientController, TestServerHubController> client) : ControllerBase
    {
        [HttpGet]
        [Route("GetInstances")]
        public async Task<IEnumerable<ClientReference>> GetInstances()
        {
            return TestServerHubController.GetClients();
        }

        [HttpGet]
        [Route("GetTemperature")]
        public async Task<int> GetClientTemperature([FromQuery] string instanceId)
        {
            return await client.GetInstance(instanceId).GetTemperature();
        }

        [HttpGet]
        [Route("ShowText")]
        public async Task ShowText([FromQuery] string instanceId)
        {
            await client.GetInstance(instanceId).ShowText();
        }

        [HttpPost]
        [Route("SendMessage")]
        public async Task SendMessage(string instanceId, string message)
        {
            client.GetInstance(instanceId).ShowMessage(message);
        }

        [HttpPost]
        [Route("ShowAndReturnMessage")]
        public async Task<string> ShowAndReturnMessage(string instanceId, string message)
        {
            return await client.GetInstance(instanceId).ShowAndReturnMessage(message);
        }

        [HttpPost]
        [Route("ShowAndReturnType")]
        public async Task<string> ShowAndReturnType(string instanceId, string message)
        {
            return client.GetInstance(instanceId).ShowAndReturnType(message);
        }

        [HttpPost]
        [Route("VariableParameters")]
        public async Task VariableParameters(string instanceId, string[] parameters)
        {
            await client.GetInstance(instanceId).VariableParameters(parameters);
            await client.GetInstance(instanceId).VariableParameters([]);
        }

        [HttpGet]
        [Route("DefaultParameters")]
        public async Task DefaultParameters(string instanceId)
        {
            await client.GetInstance(instanceId).DefaultParameters();
            //await client.GetInstance(instanceId).DefaultParameters("Test");
        }

        [HttpGet]
        [Route("NullableParameters")]
        public async Task NullableParameters(string instanceId)
        {
            await client.GetInstance(instanceId).NullableParameters(null);
            await client.GetInstance(instanceId).NullableParameters("hola");
        }


        [HttpPost]
        [Route("TestClass")]
        public async Task TestClass(string instanceId, TestClass parameter)
        {
            await client.GetInstance(instanceId).TestClass(parameter);
        }

        [HttpPost]
        [Route("NullableTestClass")]
        public async Task NullableTestClass(string instanceId, TestClass? parameter)
        {
            await client.GetInstance(instanceId).NullableTestClass(parameter);
        }

        [HttpPost]
        [Route("DefaultNullableTestClass")]
        public async Task DefaultNullableTestClass(string instanceId)
        {
            await client.GetInstance(instanceId).DefaultNullableTestClass();
        }

        [HttpPost]
        [Route("TestClassList1")]
        public async Task TestClassList1(string instanceId, List<TestClass>? parameter = null)
        {
            await client.GetInstance(instanceId).TestClassList1(parameter);
        }

        [HttpPost]
        [Route("TestClassList2")]
        public async Task TestClassList2(string instanceId, Dictionary<string, TestClass>? parameter = null)
        {
            await client.GetInstance(instanceId).TestClassList2(parameter);
        }

        [HttpPost]
        [Route("TestClassList3")]
        public async Task TestClassList3(string instanceId, HashSet<TestClass>? parameter = null)
        {
            await client.GetInstance(instanceId).TestClassList3(parameter);
        }
    }
}
