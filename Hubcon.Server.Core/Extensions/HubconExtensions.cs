using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Hubcon.Server.Core.Extensions
{
    public static class HubconExtensions
    {
        private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> _propertyCache = new();
        private static readonly ConcurrentDictionary<Type, Type> _contractCache = new();
        private static readonly ConcurrentDictionary<PropertyInfo, bool> _isSubCache = new();

        private static bool IsSub(PropertyInfo prop)
        {
            return _isSubCache.GetOrAdd(prop, t =>
            {
                return prop.PropertyType.IsAssignableTo(typeof(ISubscription))
                        && prop.ReflectedType!.IsAssignableTo(typeof(IControllerContract));
            });
        }

        public static Action<object, object?> CreateFastSetter(this PropertyInfo prop)
        {
            // 1. Buscar el campo (igual que antes)
            var field = prop.DeclaringType?.GetField($"<{prop.Name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            if (field == null)
            {
                throw new InvalidOperationException($"No se encontró el backing field para {prop.Name}");
            }

            // 2. Crear un método dinámico: (nombre, retorno, parámetros, módulo propietario)
            var method = new DynamicMethod(
                $"Set_{prop.Name}",
                null,
                new[] { typeof(object), typeof(object) },
                prop.DeclaringType!.Module,
                true); // El 'true' permite saltarse chequeos de visibilidad (acceso a campos privados)

            var il = method.GetILGenerator();

            // 3. Escribir el IL (Lenguaje Intermedio)
            il.Emit(OpCodes.Ldarg_0); // Cargar la instancia (el controller)
            il.Emit(OpCodes.Castclass, prop.DeclaringType!); // Castear a su tipo real
            il.Emit(OpCodes.Ldarg_1); // Cargar el valor a asignar (la suscripción)
            il.Emit(OpCodes.Unbox_Any, prop.PropertyType); // Unbox o cast al tipo de la propiedad
            il.Emit(OpCodes.Stfld, field); // ALMACENAR EN EL CAMPO (stfld no tiene restricción de readonly aquí)
            il.Emit(OpCodes.Ret); // Retornar

            // 4. Crear el delegado
            return (Action<object, object?>)method.CreateDelegate(typeof(Action<object, object?>));
        }

        private static List<PropertyInfo> GetProps(Type type)
        {
            return _propertyCache.GetOrAdd(type, t =>
            {
                var allProps = new List<PropertyInfo>();

                // Propiedades del propio tipo
                allProps.AddRange(t
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(prop =>
                        Attribute.IsDefined(prop, typeof(HubconInjectAttribute)) ||
                        prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                );

                // Propiedades explícitas de la base (si existe)
                if (t.BaseType != null)
                {
                    allProps.AddRange(t.BaseType
                        .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                        .Where(prop =>
                            Attribute.IsDefined(prop, typeof(HubconInjectAttribute)) ||
                            prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                    );
                }

                return allProps;
            });
        }

        private static Type GetContractType(Type type)
        {
            return _contractCache.GetOrAdd(type, t =>
            {
                return t.GetInterfaces()?
                        .ToList()?
                        .Find(x => x.IsAssignableTo(typeof(IControllerContract)))!;
            });
        }
    }
}
