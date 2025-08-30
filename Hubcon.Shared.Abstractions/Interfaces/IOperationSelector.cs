using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationSelector<T> : Hubcon.Shared.Abstractions.Standard.Interfaces.IOperationSelector<T>
    {
        IOperationConfigurator Configure<TResult>(Expression<Func<T, TResult>> expression);
        IOperationConfigurator Configure(Expression<Func<T, Delegate>> expression);
    }
}
