using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.CustomAttributes
{
    public enum MethodType
    {
        Query,
        Mutation,
        Subscription
    }

    internal class HubconMethodAttribute : Attribute
    {
        public HubconMethodAttribute(MethodType methodType)
        {
            MethodType = methodType;
        }

        public MethodType MethodType { get; }
    }
}
