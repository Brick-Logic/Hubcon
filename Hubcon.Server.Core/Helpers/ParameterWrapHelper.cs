using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        string typeName = $"{methodInfo.DeclaringType?.Name}_{methodInfo.Name}_{Guid.NewGuid().ToString()}_RequestWrapper";

        if (_cache.TryGetValue(typeName, out var cachedType)) return cachedType;

        var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

        foreach (var param in methodInfo.GetParameters())
        {
            // Pasamos el ParameterInfo completo para poder extraer sus atributos
            CreateProperty(typeBuilder, param);
        }

        var generatedType = typeBuilder.CreateType()!;
        _cache[typeName] = generatedType;
        return generatedType;
    }

    private static void CreateProperty(TypeBuilder tb, ParameterInfo param)
    {
        string propertyName = param.Name ?? "arg";
        Type propertyType = param.ParameterType;

        var fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
        var propBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

        // --- CLONACIÓN MEJORADA ---
        foreach (var attrData in param.GetCustomAttributesData())
        {
            try
            {
                var constructorArgs = attrData.ConstructorArguments
                    .Select(a => a.Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> coll
                        ? coll.Select(c => c.Value).ToArray() : a.Value).ToArray();

                // Separamos Propiedades de Campos (Named Arguments)
                var namedProperties = attrData.NamedArguments.Where(n => !n.IsField).ToList();
                var namedFields = attrData.NamedArguments.Where(n => n.IsField).ToList();

                var propInfos = namedProperties.Select(n => (PropertyInfo)n.MemberInfo).ToArray();
                var propValues = namedProperties.Select(n => n.TypedValue.Value).ToArray();

                var fieldInfos = namedFields.Select(n => (FieldInfo)n.MemberInfo).ToArray();
                var fieldValues = namedFields.Select(n => n.TypedValue.Value).ToArray();

                var cab = new CustomAttributeBuilder(
                    attrData.Constructor,
                    constructorArgs,
                    propInfos,
                    propValues,
                    fieldInfos,
                    fieldValues
                );

                propBuilder.SetCustomAttribute(cab);
            }
            catch { /* Ignorar atributos incompatibles */ }
        }

        // 2. NUEVO: Clonar el valor por defecto para OpenAPI/Swagger
        if (param.HasDefaultValue)
        {
            try
            {
                // El atributo DefaultValue acepta un object, por lo que sirve para
                // strings, ints, bools, nulls, etc.
                var ctor = typeof(DefaultValueAttribute).GetConstructor(new[] { typeof(object) });
                if (ctor != null)
                {
                    var attrBuilder = new CustomAttributeBuilder(ctor, new[] { param.DefaultValue });
                    propBuilder.SetCustomAttribute(attrBuilder);
                }
            }
            catch
            {
                // Si por alguna razón el valor por defecto no es serializable, ignoramos
            }
        }

        // --- RESTO DE LA LÓGICA (GETTER Y SETTER) SE MANTIENE IGUAL ---

        var getMethodBuilder = tb.DefineMethod("get_" + propertyName,
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType, Type.EmptyTypes);
        var getIl = getMethodBuilder.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getIl.Emit(OpCodes.Ret);

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
