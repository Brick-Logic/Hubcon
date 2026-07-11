using System.ComponentModel.DataAnnotations;
using HubconTestClient.Models;
using HubconTestDomain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HubconTest.ContractHandlers;

public class HubconTestContractHandler : IHubconTestContract
{ 
    public async Task<string> TestMethod()
    {
        return "texto de prueba";
    }
}