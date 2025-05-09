using HotChocolate.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMethodResponse : IResponse
    {
        public object? Data { get; }
    }

    public interface IMethodResponse<T> : IMethodResponse
    {
        public new T? Data { get; }
    }

    public interface IObjectMethodResponse : IMethodResponse<object>, IResponse
    {
    }
}