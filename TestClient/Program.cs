namespace TestClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            TestHubController controller = new TestHubController("http://localhost:5237/clienthub");
            await controller.StartAsync();
        }
    }
}
