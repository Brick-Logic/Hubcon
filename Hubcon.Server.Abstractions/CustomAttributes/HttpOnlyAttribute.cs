using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class HttpAttribute : Attribute, ITransportAttribute
    {
        public readonly static ITransportAttribute Default = new HttpAttribute();

        public string TransportKey => "Http";
    }
}