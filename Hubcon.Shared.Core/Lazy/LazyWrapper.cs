using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hubcon.Shared.Core.Lazy
{
    public class LazyWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : ILazyWrapper where T : notnull
    {
        private Lazy<T>? _lazy;

        public T1 GetValue<T1>(IServiceProvider sp)
        {
            _lazy ??= new Lazy<T>(sp.GetRequiredService<T>);

            if (_lazy.IsValueCreated)
                return (T1)(object)_lazy.Value;

            return (T1)(object)_lazy.Value;
        }
    }
}