using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Helpers
{
    public static class ProxyFactory
    {
        private static readonly ModuleBuilder _moduleBuilder;
        private static readonly Dictionary<string, Type> _cache = new();

        static ProxyFactory()
        {
            var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RpcProxyAssembly"), AssemblyBuilderAccess.Run);
            _moduleBuilder = ab.DefineDynamicModule("MainProxyModule");
        }

        public static (object Instance, MethodInfo Method) CreateProxyInstance(MethodInfo originalMethod, Type wrapperType)
        {
            var typeBuilder = _moduleBuilder.DefineType($"Proxy_{Guid.NewGuid():N}", TypeAttributes.Public | TypeAttributes.Class);

            var methodBuilder = typeBuilder.DefineMethod("InvokeRpc",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                originalMethod.ReturnType,
                new[] { wrapperType });

            // 1. Definir el parámetro (índice 1 porque el 0 es 'this')
            var parameterBuilder = methodBuilder.DefineParameter(1, ParameterAttributes.None, "request");

            // 2. Localizar el constructor de [FromBody]
            ConstructorInfo fromBodyCtor = typeof(FromBodyAttribute).GetConstructor(Type.EmptyTypes)!;

            // 3. Crear el constructor del atributo y asignarlo al parámetro
            CustomAttributeBuilder customAttributeBuilder = new CustomAttributeBuilder(fromBodyCtor, Array.Empty<object>());
            parameterBuilder.SetCustomAttribute(customAttributeBuilder);

            var il = methodBuilder.GetILGenerator();

            // Lógica de retorno (Default)
            if (originalMethod.ReturnType != typeof(void))
            {
                if (originalMethod.ReturnType.IsValueType)
                {
                    var local = il.DeclareLocal(originalMethod.ReturnType);
                    il.Emit(OpCodes.Ldloca_S, local);
                    il.Emit(OpCodes.Initobj, originalMethod.ReturnType);
                    il.Emit(OpCodes.Ldloc_0);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }
            }
            il.Emit(OpCodes.Ret);

            var proxyType = typeBuilder.CreateType()!;
            var instance = Activator.CreateInstance(proxyType)!;
            var method = proxyType.GetMethod("InvokeRpc")!;

            return (instance, method);
        }

        public static Type CreateWrapperType(MethodInfo methodInfo)
        {
            string typeName = $"{methodInfo.DeclaringType?.Name}_{methodInfo.Name}_RequestWrapper";

            if (_cache.TryGetValue(typeName, out var cachedType)) return cachedType;

            var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

            foreach (var param in methodInfo.GetParameters())
            {
                CreateProperty(typeBuilder, param.Name ?? "arg", param.ParameterType);
            }

            var generatedType = typeBuilder.CreateType()!;
            _cache[typeName] = generatedType;
            return generatedType;
        }


        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            var fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            var propBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

            // Getter
            var getMethodBuilder = tb.DefineMethod("get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType, Type.EmptyTypes);
            var getIl = getMethodBuilder.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            // Setter
            var setMethodBuilder = tb.DefineMethod("set_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null, new[] { propertyType });
            var setIl = setMethodBuilder.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);
            setIl.Emit(OpCodes.Ret);

            propBuilder.SetGetMethod(getMethodBuilder);
            propBuilder.SetSetMethod(setMethodBuilder);
        }
    }
}
