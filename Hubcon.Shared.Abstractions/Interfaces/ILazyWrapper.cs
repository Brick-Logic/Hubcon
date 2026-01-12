using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ILazyWrapper
    {
        public T GetValue<T>(IServiceProvider sp);
    }
}