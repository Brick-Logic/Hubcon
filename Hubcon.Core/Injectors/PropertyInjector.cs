using Hubcon.Core.Abstractions.Interfaces;
using System.Reflection;

namespace Hubcon.Core.Injectors
{
    public class PropertyInjector : IPropertyInjector
    {
        IServiceProvider _serviceProvider;
        private readonly object _instance;
        private readonly PropertyInfo _propertyInfo;

        public PropertyInjector(IServiceProvider serviceProvider, object instance, PropertyInfo propertyInfo)
        {
            _serviceProvider = serviceProvider;
            _instance = instance;
            _propertyInfo = propertyInfo;
        }

        public void WithValue(object? value)
        {
            var setMethod = _propertyInfo!.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(_instance, new[] { value });
            }
            else
            {
                // Si no tiene setter, usamos el campo backing
                var field = _propertyInfo!.DeclaringType?.GetField($"<{_propertyInfo.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                field?.SetValue(_instance, value);
            }
        }

        public void WithType<TType>() => WithType(typeof(TType));

        public void WithType(Type type)
        {
            var value = _serviceProvider.GetService(type);
            _serviceProvider.GetServiceWithInjector(value);

            // Intentamos obtener el setter (aunque sea privado)
            var setMethod = _propertyInfo!.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(_instance, new[] { value });
            }
            else
            {
                // Si no tiene setter, usamos el campo backing
                var field = _propertyInfo!.DeclaringType?.GetField($"<{_propertyInfo.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                field?.SetValue(_instance, value);
            }
        }
    }
}
