using Hubcon.Core.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Invocation
{
    public class BaseMethodResponse : BaseResponse, IObjectMethodResponse, IResponse
    {
        public object? Data { get; }

        public override bool Success { get; }

        public override string? Error { get; }

        public BaseMethodResponse(bool success, object? data = null, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }

    public class BaseMethodResponse<T> : BaseResponse, IMethodResponse, IMethodResponse<T>, IResponse
    {
        public override bool Success { get; }
        public override string? Error { get; }
        public T? Data { get; }
        object? IMethodResponse.Data => Data;

        public BaseMethodResponse(bool success, T? data = default, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }
}
