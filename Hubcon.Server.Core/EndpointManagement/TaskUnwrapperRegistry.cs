using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hubcon
{
    public static class TaskUnwrapperRegistry
    {
        private static IReadOnlyDictionary<Type, Func<Task, object?>>? _unwrappers;
        
        public static void Setup(IReadOnlyDictionary<Type, Func<Task, object?>> handlers)
        {
            _unwrappers = handlers;
        }
        
        public static object? Unwrap(Task task)
        {
            if (_unwrappers == null)
            {
                throw new InvalidOperationException(
                    "Hubcon Error: El registro de unwrappers no fue inicializado. Asegurate de que el Source Generator esté activo.");
            }

            var type = task.GetType().GenericTypeArguments[0];
            if (_unwrappers.TryGetValue(type, out var extractor))
            {
                return extractor(task);
            }

            throw new KeyNotFoundException(
                $"Hubcon Error: No se encontró un extractor nativo para Task<{type.Name}>. " +
                $"Verificá que el endpoint esté correctamente expuesto en tu controlador.");
        }
    }
}