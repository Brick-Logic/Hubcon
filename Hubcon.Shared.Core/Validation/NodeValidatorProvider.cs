using System;

namespace Hubcon.Validation
{
    public static class NodeValidatorProvider
    {
        private static Func<Type, object?>? _getValidatorNodeDelegate;
    
        public static void Setup(Func<Type, object?> getValidatorNodeDelegate)
        {
            _getValidatorNodeDelegate ??= getValidatorNodeDelegate;
        }

        public static ValidatorNode<T>? GetValidator<T>()
        {
            return (ValidatorNode<T>?)_getValidatorNodeDelegate?.Invoke(typeof(T));
        }
    }
}