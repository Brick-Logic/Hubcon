using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.CustomAttributes
{
    public enum MethodType
    {
        Query,
        Mutation,
        Subscription
    }

    public class HubconMethodAttribute : Attribute
    {
        public HubconMethodAttribute(MethodType methodType)
        {
            MethodType = methodType;
        }

        public MethodType MethodType { get; }
    }
}
