namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMethodDescriptorProvider
    {
        event Action<IMethodDescriptor>? OnMethodRegistered;
        bool GetMethodDescriptor(IMethodInvokeRequest request, out IMethodDescriptor? value);
        void RegisterMethods(Type controllerType);
    }
}
