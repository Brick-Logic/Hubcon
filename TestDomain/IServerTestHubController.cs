using Hubcon.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDomain
{
    public interface IServerTestHubController : IServerHubController
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }
}
