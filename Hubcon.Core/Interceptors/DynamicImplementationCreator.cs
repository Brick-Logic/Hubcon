using System.Reflection;
using System.Reflection.Emit;

namespace Hubcon.Core.Interceptors
{
    internal static class DynamicImplementationCreator
    {
        private readonly static Dictionary<Type, Type> BuiltTypes = new();

        internal static object CreateImplementation(Type interfaceType)
        {
            if (BuiltTypes.TryGetValue(interfaceType, out var type))
                return Activator.CreateInstance(type)!;

            // Crear un Assembly dinámico
            AssemblyName assemblyName = new("DynamicAssembly");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            // Crear un módulo dinámico
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            // Crear una clase dinámica que implemente la interfaz
            var typeBuilder = moduleBuilder.DefineType(
                "DynamicClass",
                TypeAttributes.Public | TypeAttributes.Class);

            // Implementar la interfaz en la clase
            typeBuilder.AddInterfaceImplementation(interfaceType);

            // Implementar cada método de la interfaz
            foreach (var method in interfaceType.GetMethods())
            {
                // Crear un método en la clase dinámica
                var methodBuilder = typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    method.ReturnType,
                    method.GetParameters().Select(p => p.ParameterType).ToArray());

                // Obtener el ILGenerator para generar la implementación del método
                var il = methodBuilder.GetILGenerator();

                // Proveer una implementación básica para métodos con retorno
                if (method.ReturnType == typeof(void))
                {
                    il.Emit(OpCodes.Ret);  // Retornar vacío para métodos void
                }
                else if (method.ReturnType.IsValueType)
                {
                    // Retornar el valor predeterminado de tipos por valor
                    var local = il.DeclareLocal(method.ReturnType);
                    il.Emit(OpCodes.Ldloca_S, local);
                    il.Emit(OpCodes.Initobj, method.ReturnType);
                    il.Emit(OpCodes.Ldloc, local);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    // Retornar null para tipos de referencia
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ret);
                }
            }

            // Crear el tipo dinámico
            var dynamicType = typeBuilder.CreateType();
            BuiltTypes.TryAdd(interfaceType, dynamicType);

            // Retornar una instancia de la clase dinámica
            return Activator.CreateInstance(dynamicType)!;
        }
    }
}
