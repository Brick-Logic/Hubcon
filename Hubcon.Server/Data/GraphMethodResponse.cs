using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Data
{
    //public class GraphMethodResponse : BaseMethodResponse
    //{
    //    public object Data

    //    public GraphMethodResponse(bool success, object? data = null) : base(success, data)
    //    {
    //    }
    //}

    public interface IGraphMethodResponse : IObjectOperationResponse
    {
        [GraphQLType(typeof(AnyType))]
        public new object? Data { get; set; }
    }
}
