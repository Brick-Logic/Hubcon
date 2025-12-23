using System;
using System.Linq.Expressions;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationSelector<T> : Standard.Interfaces.IOperationSelector<T>
    {
        IOperationConfigurator Configure<TResult>(Expression<Func<T, TResult>> expression);
        IOperationConfigurator Configure(Expression<Func<T, Delegate>> expression);
    }
}
