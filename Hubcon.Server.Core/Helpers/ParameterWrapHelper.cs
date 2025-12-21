using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;


namespace Hubcon.Server.Core.Helpers;

public static class ParameterWrapHelper
{
    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly Dictionary<string, Type> _cache = new();

    static ParameterWrapHelper()
    {
        var assemblyName = new AssemblyName("RpcDynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainWrapperModule");
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
