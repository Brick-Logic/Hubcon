using Hubcon.Shared.Abstractions.Standard.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;


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

    public static Type CreateWrapperType(MethodInfo methodInfo, Func<ParameterInfo, bool>? typeExclusionExpression = null)
    {
        string typeName = $"{methodInfo.Name}Request_{methodInfo.GetMethodSignature(true)}";

        if (_cache.TryGetValue(typeName, out var cachedType)) return cachedType;

        var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        var parameters = methodInfo.GetParameters();

        foreach (var param in parameters)
        {
            // --- CAMBIO AQUÍ: Lógica de Field vs Property ---
            if (param.ParameterType == typeof(CancellationToken))
            {
                // Definimos un Field. Minimal API y System.Text.Json ignoran los fields por defecto.
                // Usamos el nombre original del parámetro para encontrarlo luego por reflexión.
                typeBuilder.DefineField(param.Name!, typeof(CancellationToken), FieldAttributes.Public);
            }
            else
            {
                var isExcluded = typeExclusionExpression?.Invoke(param) ?? false;
                CreateProperty(typeBuilder, param, isExcluded);
            }
        }

        if (parameters.Length > 0)
        {
            // 1. Definimos el tipo genérico cerrado usando el Builder
            Type valueTaskType = typeof(ValueTask<>).MakeGenericType(typeBuilder);

            // 2. Definimos el constructor por defecto
            var defaultCtor = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            // 3. Constructor de ValueTask<T>
            ConstructorInfo genericCtor = typeof(ValueTask<>).GetConstructors()
                .First(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType.IsGenericParameter);

            ConstructorInfo valueTaskCtor = TypeBuilder.GetConstructor(valueTaskType, genericCtor);

            // 4. Implementación de TryParse (Pase VIP para el ruteo)
            var tryParseBuilder = typeBuilder.DefineMethod(
                "TryParse",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(bool),
                new[] { typeof(string), typeBuilder.MakeByRefType() }
            );

            tryParseBuilder.DefineParameter(1, ParameterAttributes.None, "s");
            tryParseBuilder.DefineParameter(2, ParameterAttributes.Out, "result");

            var il = tryParseBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, defaultCtor);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
        }

        var generatedType = typeBuilder.CreateType()!;
        _cache[typeName] = generatedType;
        return generatedType;
    }

    private static void CreateProperty(TypeBuilder tb, ParameterInfo param, bool isExcluded)
    {
        string propertyName = param.Name ?? "arg";
        Type propertyType = param.ParameterType;
        var fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
        var propBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

        var attributes = param.GetCustomAttributesData();
        // --- CLONACIÓN MEJORADA ---
        foreach (var attrData in attributes)
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

        if (isExcluded)
        {
            var ignoreCtor = typeof(IgnoreDataMemberAttribute).GetConstructor(Type.EmptyTypes);
            var attrBuilder = new CustomAttributeBuilder(ignoreCtor!, Array.Empty<object>());
            propBuilder.SetCustomAttribute(attrBuilder);

            var ignoreCtor2 = typeof(BindNeverAttribute).GetConstructor(Type.EmptyTypes);
            var attrBuilder2 = new CustomAttributeBuilder(ignoreCtor2!, Array.Empty<object>());
            propBuilder.SetCustomAttribute(attrBuilder2);
        }

        // --- RESTO DE LA LÓGICA (GETTER Y SETTER) SE MANTIENE IGUAL ---

        MethodAttributes visibility = MethodAttributes.Public;

        //if (param.ParameterType == typeof(CancellationToken))
        //{
        //    visibility = MethodAttributes.Private;
        //}
        //else
        //{
        //    visibility = MethodAttributes.Public;
        //}

        var getMethodBuilder = tb.DefineMethod("get_" + propertyName,
            visibility | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType, Type.EmptyTypes);
        var getIl = getMethodBuilder.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getIl.Emit(OpCodes.Ret);

        var setMethodBuilder = tb.DefineMethod("set_" + propertyName,
            visibility | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
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
