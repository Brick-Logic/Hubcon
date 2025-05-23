using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IMethodDescriptorProvider
    {
        event Action<IMethodDescriptor>? OnMethodRegistered;
        bool GetMethodDescriptor(IOperationRequest request, out IMethodDescriptor? value);
        void RegisterMethods(Type controllerType);
    }
}
