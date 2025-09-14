using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Models
{
    public interface IInvocationContext
    {
        public IServiceProvider Services { get; }
        public IOperationRequest Request { get; }
        public CancellationToken CancellationToken { get; }
        public object? Result { get; set; }
        public bool IsSuccess { get; }
        public string Error { get; }
        public Exception? Exception { get; }
    }

    public sealed record class InvocationContext : IInvocationContext
    {
        public IServiceProvider Services { get; set; }
        public IOperationRequest Request { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public object? Result { get; set; }
        public bool IsSuccess { get; set; }
        public string Error { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}