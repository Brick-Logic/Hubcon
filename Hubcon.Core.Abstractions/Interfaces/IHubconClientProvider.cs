using Hubcon.Core.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IHubconClientProvider
    {
        TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
