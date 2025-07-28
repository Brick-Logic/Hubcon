using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Client.Core.Configurations
{
    public class GlobalOperationConfigurator<T> : IGlobalOperationConfigurator<T>, IGlobalOperationOptions
    {
        public GlobalOperationConfigurator(Dictionary<string, IOperationOptions> operationOptions)
        {
            OperationOptions = operationOptions;
        }

        public Dictionary<string, IOperationOptions> OperationOptions { get; }

        public IOperationConfigurator Configure(Expression<Func<T, object>> expression)
        {
            var memberInfo = ExtractMemberInfo(expression);

            if (memberInfo == null)
                throw new ArgumentException($"Only property or method expressions from type {typeof(T).Name} are allowed.");

            if (!IsFromTType(memberInfo))
                throw new ArgumentException($"Only property or method expressions from type {typeof(T).Name} are allowed.");

            string key = GetMemberKey(memberInfo);

            if (OperationOptions.TryGetValue(key, out var existingOptions))
            {
                return (IOperationConfigurator)existingOptions;
            }

            var options = new OperationOptions(memberInfo);
            OperationOptions.TryAdd(key, options);
            return options;
        }

        private MemberInfo ExtractMemberInfo(Expression<Func<T, object>> expression)
        {
            Expression body = expression.Body;

            // Caso 1: Acceso directo a propiedad o campo
            if (body is MemberExpression memberExpr && memberExpr.Member is PropertyInfo or FieldInfo)
            {
                return memberExpr.Member;
            }

            // Caso 3: Method Group - ruta específica Body.Operand.Object.Value
            if (body is UnaryExpression unary &&
                unary.Operand is MethodCallExpression operandMethodCall &&
                operandMethodCall.Object is ConstantExpression constExpr &&
                constExpr.Value is MethodInfo methodFromGroup)
            {
                return methodFromGroup;
            }

            // Fallback: búsqueda general
            var methodInfo = FindMethodInfoInExpression(body);
            if (methodInfo != null)
                return methodInfo;

            return null;
        }

        private MethodInfo FindMethodInfoInExpression(Expression expression)
        {
            switch (expression)
            {
                // Caso: ConstantExpression que contiene directamente un MethodInfo
                case ConstantExpression constExpr when constExpr.Value is MethodInfo directMethod:
                    return directMethod;

                // Caso: ConstantExpression con objeto que contiene información del método
                case ConstantExpression constExpr when constExpr.Value != null:
                    return ExtractMethodFromConstantValue(constExpr.Value);

                // Caso: MemberExpression accediendo a un miembro
                case MemberExpression memberExpr:
                    // Si es acceso a la propiedad "Method" de un delegate
                    if (memberExpr.Member.Name == "Method" && memberExpr.Expression != null)
                    {
                        return FindMethodInfoInExpression(memberExpr.Expression);
                    }
                    // Si el miembro mismo es un MethodInfo
                    if (memberExpr.Member is MethodInfo method)
                    {
                        return method;
                    }
                    break;

                // Caso: MethodCallExpression - buscar en argumentos
                case MethodCallExpression methodCall:
                    // Si es CreateDelegate, buscar en el tercer argumento
                    if (methodCall.Method.Name == "CreateDelegate" && methodCall.Arguments.Count >= 3)
                    {
                        return FindMethodInfoInExpression(methodCall.Arguments[2]);
                    }

                    // Buscar en todos los argumentos
                    foreach (var arg in methodCall.Arguments)
                    {
                        var result = FindMethodInfoInExpression(arg);
                        if (result != null)
                            return result;
                    }
                    break;

                // Caso: UnaryExpression - buscar en el operando
                case UnaryExpression unary:
                    return FindMethodInfoInExpression(unary.Operand);
            }

            return null;
        }

        private MethodInfo ExtractMethodFromConstantValue(object constantValue)
        {
            if (constantValue is MethodInfo methodInfo)
                return methodInfo;

            // Buscar en los campos del objeto si es una clase generada por el compilador
            var type = constantValue.GetType();

            // Clases generadas por el compilador para capturar variables
            if (type.Name.Contains("DisplayClass") || type.Name.Contains("c__"))
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var value = field.GetValue(constantValue);
                    if (value is MethodInfo method)
                        return method;

                    // Buscar recursivamente si el campo contiene otro objeto
                    if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                    {
                        var nestedMethod = ExtractMethodFromConstantValue(value);
                        if (nestedMethod != null)
                            return nestedMethod;
                    }
                }
            }

            return null;
        }

        private static bool IsFromTType(MemberInfo memberInfo)
        {
            return memberInfo.DeclaringType == typeof(T);
        }

        private string GetMemberKey(MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                MethodInfo method => method.GetMethodSignature(),
                PropertyInfo prop => prop.Name,
                FieldInfo field => field.Name,
                _ => memberInfo.Name
            };
        }
    }
}
