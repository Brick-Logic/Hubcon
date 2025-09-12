using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Helpers
{
    public static class ControllerContractHelper
    {
        public static IEnumerable<Type> FindImplementations(Assembly rootAssembly, List<Type> excludedTypes)
        {
            // 1. Buscar la interfaz base
            var baseInterface = typeof(IControllerContract);

            // 2. Obtener todos los assemblies (el actual + referenciados)
            var assemblies = new HashSet<Assembly>();
            CollectAssemblies(rootAssembly, assemblies);

            // 3. Buscar todas las clases que implementen una interfaz hija de IControllerContract
            var implementations = assemblies
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return []; // Ignorar assemblies que no se puedan cargar
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => !excludedTypes.Any(x => x.IsAssignableFrom(t)))
                .Where(t =>
                {
                    var interfaces = t.GetInterfaces();
                    return interfaces.Any(i =>
                        i != baseInterface && baseInterface.IsAssignableFrom(i));
                });

            return implementations;
        }

        private static void CollectAssemblies(Assembly root, HashSet<Assembly> collected)
        {
            if (!collected.Add(root)) return;

            foreach (var reference in root.GetReferencedAssemblies())
            {
                try
                {
                    var asm = Assembly.Load(reference);
                    CollectAssemblies(asm, collected);
                }
                catch
                {
                    // Ignorar referencias que no se puedan cargar
                }
            }
        }
    }
}
