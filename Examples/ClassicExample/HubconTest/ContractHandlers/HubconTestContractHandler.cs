using HubconTestClient.Models;

namespace HubconTest.ContractHandlers;

public class HubconTestContractHandler : IHubconTestContract
{
    public async Task<string> TestMethod()
    {
        return "texto de prueba";
    }
}