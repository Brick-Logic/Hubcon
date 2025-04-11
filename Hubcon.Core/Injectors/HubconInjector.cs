using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Injectors
{
    public class DependencyInjector<T, TProp>
    {
        IServiceProvider _serviceProvider;
        private readonly object _instance;

        public DependencyInjector(IServiceProvider serviceProvider, object instance)
        {
            _serviceProvider = serviceProvider;
            _instance = instance;
        }

        public PropertyInjector Inject(Expression<Func<T, TProp>> propertyExpression)
        {
            var memberExpression = (MemberExpression)propertyExpression.Body;
            PropertyInfo propertyInfo = (PropertyInfo)memberExpression.Member;

            return new PropertyInjector(_serviceProvider, _instance, propertyInfo);
        }

        public PropertyInjector Inject(PropertyInfo propertyInfo)
        {
            return new PropertyInjector(_serviceProvider, _instance, propertyInfo);
        }

        public class PropertyInjector
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


    public static class HubconInjector
    {
        public static bool GetServiceWithInjector<T>(this IServiceProvider serviceProvider, T instance, Action<DependencyInjector<T, object?>>? configure = null)
        {
            var injector = new DependencyInjector<T, object?>(serviceProvider, instance!);

            configure?.Invoke(injector);

            var properties = instance!.GetType().BaseType!
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(prop => Attribute.IsDefined(prop, typeof(HubconInjectAttribute)));

            foreach (var property in properties)
            {
                if (property.GetValue(instance) != null)
                    continue;

                injector.Inject(property).WithType(property.PropertyType);
            }

            return true;
        }
    }
}
