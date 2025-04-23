using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HubconAnalyzer.SourceGenerators
{
    [Generator]
    public class CommunicationProxyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var interfaces = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is InterfaceDeclarationSyntax,
                    transform: (ctx, _) =>
                    {
                        var iface = (InterfaceDeclarationSyntax)ctx.Node;
                        var symbol = ctx.SemanticModel.GetDeclaredSymbol(iface) as INamedTypeSymbol;

                        // Chequeamos que implemente ICommunicationContract
                        if (symbol == null)
                            return null;

                        var implementsContract = symbol.AllInterfaces
                            .Any(i => i.Name == "ICommunicationContract"); // Puedes cambiar esto por el nombre completo

                        return implementsContract ? symbol : null;
                    })
                .Where( symbol => symbol != null)
                .Collect();

            context.RegisterSourceOutput(interfaces, (spc, interfaceList) =>
            {
                foreach (var iface in interfaceList)
                {
                    var code = GenerateProxyClass(iface);
                    var filename = $"{iface.Name}Proxy.g.cs";
                    spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
                }
            });
        }

        private static string GenerateProxyClass(INamedTypeSymbol iface)
        {
            var ns = iface.ContainingNamespace.ToDisplayString();
            var proxyName = iface.Name + "Proxy";

            var sb = new StringBuilder();

            sb.AppendLine($"#nullable enable");
            sb.AppendLine($"using Castle.DynamicProxy;");
            sb.AppendLine($"using Hubcon.Core.Models.Invocation;");
            sb.AppendLine($"using System.Reflection;");
            sb.AppendLine($"");
            sb.AppendLine($"public class {proxyName} : {iface.ToDisplayString()}");
            sb.AppendLine($"{{");
            sb.AppendLine($"    public AsyncInterceptorBase Interceptor;");
            sb.AppendLine($"");
            sb.AppendLine($"    public {proxyName}(AsyncInterceptorBase interceptor)");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        Interceptor = interceptor;");
            sb.AppendLine($"    }}");
            sb.AppendLine($"");

            foreach (var method in iface.GetMembers().OfType<IMethodSymbol>())
            {
                var returnType = method.ReturnType.ToDisplayString();
                var methodName = method.Name;
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var paramNames = string.Join(", ", method.Parameters.Select(p => p.Name));
                
               
                sb.AppendLine($"    public {returnType} {methodName}({parameters})");
                sb.AppendLine($"    {{");

                sb.AppendLine($"        MethodInfo method = typeof({iface.ToDisplayString()}).GetMethod(\"{methodName}\")!;");

                if(paramNames.Any())
                    sb.AppendLine($"        SimpleInvocation invocation = new SimpleInvocation(this, Interceptor, method, {string.Join(",", paramNames)});");
                else
                    sb.AppendLine($"        SimpleInvocation invocation = new SimpleInvocation(this, Interceptor, method);");

                if (returnType == "void")
                {
                    sb.AppendLine($"        Interceptor.InterceptSynchronous(invocation);");
                }
                else if(returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    var generic = returnType.Replace("System.Threading.Tasks.Task<", "").TrimEnd('>');
                    sb.AppendLine($"        Interceptor.InterceptAsynchronous<{generic}>(invocation);");
                    sb.AppendLine($"        return ({returnType})invocation.ReturnValue!;");
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task"))
                {
                    sb.AppendLine($"        Interceptor.InterceptAsynchronous(invocation);");
                    sb.AppendLine($"        return ({returnType})invocation.ReturnValue!;");
                }
                else
                {
                    sb.AppendLine($"        Interceptor.InterceptSynchronous(invocation);");
                    sb.AppendLine($"        return ({returnType})invocation.ReturnValue!;");
                }

                sb.AppendLine($"    }}");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}