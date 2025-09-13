using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;


namespace HubconTest
{
    public static class MinimalApiWrapperHelper
    {
        private static AssemblyBuilder _asmBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DynamicMinimalApiWrappers"), AssemblyBuilderAccess.Run);
        private static ModuleBuilder _moduleBuilder = _asmBuilder.DefineDynamicModule("MainModule");

        public static Delegate CreateWrapperDelegate(MethodInfo original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            var parameters = original.GetParameters();
            var paramTypes = parameters.Select(p => p.ParameterType).ToArray();
            var returnType = original.ReturnType;

            // Crear tipo dinámico único
            var typeBuilder = _moduleBuilder.DefineType(
                "Wrapper_" + Guid.NewGuid().ToString("N"),
                TypeAttributes.Public | TypeAttributes.Class);

            // Crear método con la misma firma
            var methodBuilder = typeBuilder.DefineMethod(
                original.Name,
                MethodAttributes.Public | MethodAttributes.HideBySig,
                returnType,
                paramTypes
            );

            // Copiar atributos de método
            foreach (var attr in original.GetCustomAttributesData())
            {
                var attrBuilder = CreateCustomAttributeBuilder(attr);
                if (attrBuilder != null)
                    methodBuilder.SetCustomAttribute(attrBuilder);
            }

            // Copiar atributos de parámetros y nombres
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var pb = methodBuilder.DefineParameter(i + 1, param.Attributes, param.Name);

                foreach (var attr in param.GetCustomAttributesData())
                {
                    var attrBuilder = CreateCustomAttributeBuilder(attr);
                    if (attrBuilder != null)
                        pb.SetCustomAttribute(attrBuilder);
                }
            }

            // Generar IL: vacío o default
            var il = methodBuilder.GetILGenerator();
            if (returnType == typeof(void))
            {
                il.Emit(OpCodes.Ret);
            }
            else
            {
                if (returnType.IsValueType)
                {
                    var local = il.DeclareLocal(returnType);
                    il.Emit(OpCodes.Ldloca_S, local);
                    il.Emit(OpCodes.Initobj, returnType);
                    il.Emit(OpCodes.Ldloc, local);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }
                il.Emit(OpCodes.Ret);
            }

            // Crear el tipo
            var wrapperType = typeBuilder.CreateType();
            var wrapperMethod = wrapperType.GetMethod(original.Name);

            Delegate del;
            if (original.IsStatic)
            {
                del = Delegate.CreateDelegate(Expression.GetDelegateType(paramTypes.Concat(new[] { returnType }).ToArray()), wrapperMethod);
            }
            else
            {
                var instance = Activator.CreateInstance(wrapperType);
                del = Delegate.CreateDelegate(Expression.GetDelegateType(paramTypes.Concat(new[] { returnType }).ToArray()), instance, wrapperMethod);
            }

            return del;
        }

        // Convierte un CustomAttributeData en CustomAttributeBuilder
        private static CustomAttributeBuilder? CreateCustomAttributeBuilder(CustomAttributeData attrData)
        {
            try
            {
                var ctorArgs = attrData.ConstructorArguments.Select(a => a.Value).ToArray();
                var namedProps = attrData.NamedArguments
                    .Where(n => n.IsField == false)
                    .Select(n => (PropertyInfo)n.MemberInfo)
                    .ToArray();
                var propValues = attrData.NamedArguments
                    .Where(n => n.IsField == false)
                    .Select(n => n.TypedValue.Value)
                    .ToArray();

                var namedFields = attrData.NamedArguments
                    .Where(n => n.IsField)
                    .Select(n => (FieldInfo)n.MemberInfo)
                    .ToArray();
                var fieldValues = attrData.NamedArguments
                    .Where(n => n.IsField)
                    .Select(n => n.TypedValue.Value)
                    .ToArray();

                return new CustomAttributeBuilder(
                    attrData.Constructor,
                    ctorArgs,
                    namedProps,
                    propValues,
                    namedFields,
                    fieldValues
                );
            }
            catch
            {
                // No se puede crear, ignorar
                return null;
            }
        }
    }

}
