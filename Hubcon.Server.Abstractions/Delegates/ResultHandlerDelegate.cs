using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Delegates
{
    public delegate Task<IOperationResult> ResultHandlerDelegate(object? result);
}