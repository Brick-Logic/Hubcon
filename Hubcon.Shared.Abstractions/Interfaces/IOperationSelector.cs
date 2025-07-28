using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IGlobalOperationConfigurator<T>
    {
        IOperationConfigurator Configure(System.Linq.Expressions.Expression<Func<T, object>> expression);
    }
}
