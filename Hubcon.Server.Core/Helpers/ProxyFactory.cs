using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public static (object Instance, MethodInfo Method) CreateProxyInstance(MethodInfo originalMethod, Type wrapperType, bool isGet)
        {
            var typeBuilder = _moduleBuilder.DefineType($"Proxy_{Guid.NewGuid():N}", TypeAttributes.Public | TypeAttributes.Class);

            // Determinamos si el método original tiene parámetros comparando con el Wrapper
            // (O chequeando si el originalMethod.GetParameters() está vacío)
            bool hasParameters = originalMethod.GetParameters().Length > 0;

            // Definimos los tipos de los parámetros del método del proxy
            Type[] proxyParamTypes = hasParameters ? new[] { wrapperType } : Type.EmptyTypes;

            var methodBuilder = typeBuilder.DefineMethod("InvokeRpc",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                originalMethod.ReturnType,
                proxyParamTypes);

            if (hasParameters)
            {
                // 1. Definir el parámetro (índice 1 porque el 0 es 'this')
                var parameterBuilder = methodBuilder.DefineParameter(1, ParameterAttributes.None, "request");

                // 2. Aplicar atributos según el verbo
                if (isGet)
                {
                    // Usamos AsParametersAttribute para que descomponga el objeto en la Query String
                    var asParamsCtor = typeof(Microsoft.AspNetCore.Http.AsParametersAttribute).GetConstructor(Type.EmptyTypes);
                    var attrBuilder = new CustomAttributeBuilder(asParamsCtor!, Array.Empty<object>());
                    parameterBuilder.SetCustomAttribute(attrBuilder);
                }
                else
                {
                    // Para POST/PUT/etc, usamos FromBody
                    var fromBodyCtor = typeof(Microsoft.AspNetCore.Mvc.FromBodyAttribute).GetConstructor(Type.EmptyTypes);
                    var attrBuilder = new CustomAttributeBuilder(fromBodyCtor!, Array.Empty<object>());
                    parameterBuilder.SetCustomAttribute(attrBuilder);
                }
            }

            var il = methodBuilder.GetILGenerator();

            // Lógica de retorno (Default para que compile, luego tú inyectarás la llamada real)
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
