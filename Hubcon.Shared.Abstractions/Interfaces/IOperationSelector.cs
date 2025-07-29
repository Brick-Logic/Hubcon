using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IGlobalOperationConfigurator<T>
    {
        IOperationConfigurator Configure(Expression<Func<T, object>> expression);
    }
}
