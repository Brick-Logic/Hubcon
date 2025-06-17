using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using HubconAnalyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace HubconAnalyzers.SourceGenerators
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
                            .Any(i => i.Name == nameof(IControllerContract)); // Puedes cambiar esto por el nombre completo

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
            var proxyName = iface.Name + "Proxy";
            var sb = new StringBuilder();

            sb.AppendLine($"#nullable enable");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Models;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interceptor;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interfaces;");
            sb.AppendLine($"using Hubcon.Shared.Core.Attributes;");
            sb.AppendLine($"using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine($"using System.Reflection;");
            sb.AppendLine($"using System.Runtime.CompilerServices;");
            sb.AppendLine($"");
            sb.AppendLine($"[HubconProxy]");
            sb.AppendLine($"public class {proxyName} : {nameof(BaseContractProxy)}, {iface.ToDisplayString()}");
            sb.AppendLine($"{{");

            foreach(var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var accessors = "get;";

                if (property.SetMethod != null)
                    accessors += " set;";

                var type = $"    public {property.Type.ToString()} {property.Name} {{ {accessors} }}";

                sb.AppendLine(type);
            }

            sb.AppendLine($"    public {proxyName}({nameof(IClientProxyInterceptor)} interceptor) : base(interceptor) {{ }}");
            sb.AppendLine($"");

            foreach (var method in iface
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
            {
                var returnType = method.ReturnType.ToDisplayString();
                var methodName = method.Name;
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var paramNames = string.Join(", ", method.Parameters.Select(p => p.Name));

                sb.AppendLine($"    public {returnType} {methodName}({parameters})");
                sb.AppendLine($"    {{");

                var stringMethodName = $"\"{method.GetMethodSignature()}\"";
                var callMethod = "";
                var AllParameters = "";

                if (paramNames.Any())
                    AllParameters = $", {string.Join(",", paramNames)}";

                if (returnType == "void")
                {
                    callMethod = $"{nameof(BaseContractProxy.CallAsync)}({stringMethodName}{AllParameters}).Wait();";
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    var generic = returnType.Replace("System.Threading.Tasks.Task<", "").TrimEnd('>');
                    callMethod = $"return {nameof(BaseContractProxy.InvokeAsync)}<{generic}>({stringMethodName}{AllParameters});";
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task"))
                {
                    callMethod = $"return {nameof(BaseContractProxy.CallAsync)}({stringMethodName}{AllParameters});";
                }
                else
                {
                    callMethod = $"return {nameof(BaseContractProxy.InvokeAsync)}<{returnType}>({stringMethodName}{AllParameters}).Result;";
                }

                sb.AppendLine($"        {callMethod}");

                sb.AppendLine($"    }}");
            }

            sb.AppendLine("}");
            sb.AppendLine("");

            var preserver = GenerateProxyPreserverClass(iface);
            sb.AppendLine(preserver);

            return sb.ToString();
        }

        private static string GenerateProxyPreserverClass(INamedTypeSymbol iface)
        {
            var sb = new StringBuilder();

            var proxyName = iface.Name + "Proxy";

            sb.AppendLine($"public static class {proxyName}PreserverModule");
            sb.AppendLine($"{{");
            sb.AppendLine($"    [ModuleInitializer]");
            sb.AppendLine($"    public static void Init()");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        _ = typeof({proxyName});");            
            sb.AppendLine($"    }}");       
            sb.AppendLine($"");
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({proxyName}))]");
            sb.AppendLine($"    public static void {proxyName}Preserver() {{ }}");                
            sb.AppendLine($"}}");

            return sb.ToString();
        }
    }
}