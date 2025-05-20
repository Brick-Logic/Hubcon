using HubconTestDomain;

namespace HubconTest.Controllers
{
    public class SecondTestController(ILogger<SecondTestController> logger) : ISecondTestContract
    {
        public async Task TestMethod()
        {
            logger.LogInformation("TestMethod called");
        }

        public async Task TestMethod(string message)
        {
            logger.LogInformation(message);
        }

        public async Task<string> TestReturn(string message)
        {
            logger.LogInformation(message);
            return message;
        }

        public void TestVoid()
        {
            logger.LogInformation("TestVoid called.");         
        }

        public string TestReturn()
        {
            logger.LogInformation("TestVoid called.");
            return "some return value";
        }
    }
}