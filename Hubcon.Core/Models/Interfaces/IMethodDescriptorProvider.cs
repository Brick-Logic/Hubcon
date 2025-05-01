using Hubcon.Core.MethodHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IMethodDescriptorProvider
    {
        event Action<MethodDescriptor>? OnMethodRegistered;
        bool GetMethodDescriptor(MethodInvokeRequest request, out MethodDescriptor? value);
        void RegisterMethods(Type controllerType);
    }
}
