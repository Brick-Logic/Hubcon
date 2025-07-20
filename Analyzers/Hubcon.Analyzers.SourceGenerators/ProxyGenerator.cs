using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using HubconAnalyzers.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HubconAnalyzers.SourceGenerators
{
    [Generator]
    public class CommunicationProxyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Capturamos interfaces del proyecto actual
            var localInterfaces = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is InterfaceDeclarationSyntax,
                    transform: (ctx, _) =>
                    {
                        var iface = (InterfaceDeclarationSyntax)ctx.Node;
                        var symbol = ctx.SemanticModel.GetDeclaredSymbol(iface) as INamedTypeSymbol;
                        return GetValidContractInterface(symbol);
                    })
                .Where(symbol => symbol != null)
                .Collect();

            // Capturamos todas las referencias de compilación para buscar interfaces en proyectos referenciados
            var referencedInterfaces = context.CompilationProvider
                .Select((compilation, _) =>
                {
                    var interfaces = new List<INamedTypeSymbol>();

                    // Recorremos todos los assemblies referenciados
                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                        {
                            CollectInterfacesFromAssembly(assembly.GlobalNamespace, interfaces);
                        }
                    }

                    return interfaces.ToArray();
                });

            // Combinamos ambos sources
            var allInterfaces = localInterfaces
                .Combine(referencedInterfaces)
                .Select((combined, _) =>
                {
                    var (local, referenced) = combined;
                    return local.Concat(referenced).Distinct(SymbolEqualityComparer.Default).ToArray();
                });

            context.RegisterSourceOutput(allInterfaces, (spc, interfaceList) =>
            {
                foreach (var iface in interfaceList.Cast<INamedTypeSymbol>())
                {
                    var code = GenerateProxyClass(iface);
                    var filename = $"{iface.Name}Proxy.g.cs";
                    spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
                }
            });
        }

        private static INamedTypeSymbol GetValidContractInterface(INamedTypeSymbol symbol)
        {
            if (symbol == null)
                return null;

            // Chequeamos que implemente IControllerContract
            var implementsContract = symbol.AllInterfaces
                .Any(i => i.Name == nameof(IControllerContract));

            return implementsContract ? symbol : null;
        }

        private static void CollectInterfacesFromAssembly(INamespaceSymbol namespaceSymbol, List<INamedTypeSymbol> interfaces)
        {
            // Recorremos todos los tipos en el namespace
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
                {
                    var validInterface = GetValidContractInterface(namedType);
                    if (validInterface != null)
                    {
                        interfaces.Add(validInterface);
                    }
                }
                else if (member is INamespaceSymbol childNamespace)
                {
                    // Recursivamente exploramos namespaces anidados
                    CollectInterfacesFromAssembly(childNamespace, interfaces);
                }
            }
        }

        private static string GenerateProxyClass(INamedTypeSymbol iface)
        {
            var proxyName = iface.Name + "Proxy";
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var sb = new StringBuilder();

            sb.AppendLine($"#nullable enable");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Models;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interceptor;");
            sb.AppendLine($"using Hubcon.Shared.Abstractions.Standard.Interfaces;");
            sb.AppendLine($"using Hubcon.Shared.Core.Attributes;");
            sb.AppendLine($"using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine($"using System.Reflection;");
            sb.AppendLine($"using System.ComponentModel;");
            sb.AppendLine($"using System.Runtime.CompilerServices;");
            sb.AppendLine($"");

            // Determinamos el nivel de indentación base
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";

            // Solo agregamos el namespace si no es el global
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine($"{{");
            }

            sb.AppendLine($"{baseIndent}[HubconProxy]");
            sb.AppendLine($"{baseIndent}[EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public class {proxyName} : {nameof(BaseContractProxy)}, {iface.ToDisplayString()}");
            sb.AppendLine($"{baseIndent}{{");

            foreach (var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                var accessors = "get;";

                if (property.SetMethod != null)
                    accessors += " set;";

                var type = $"{baseIndent}    public {property.Type.ToString()} {property.Name} {{ {accessors} }}";

                sb.AppendLine(type);
            }       

            foreach (var method in iface
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
            {
                var returnType = method.ReturnType.ToDisplayString();
                var methodName = method.Name;
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var paramNames = string.Join(", ", method.Parameters.Select(p => p.Name));

                sb.AppendLine($"{baseIndent}    public {returnType} {methodName}({parameters})");
                sb.AppendLine($"{baseIndent}    {{");

                var stringMethodName = $"\"{method.GetMethodSymbolSignature()}\"";
                var callMethod = "";

                var AllParameters = "";

                if (method.Parameters.Any())
                {
                    AllParameters = $", new Dictionary<string, object?>() {{ ";
                    bool first = true;

                    foreach (var param in method.Parameters)
                    {
                        if (first)
                        {
                            AllParameters += $"{{ \"{param.Name}\", {param.Name} }}";
                            first = false;
                        }
                        else
                        {
                            AllParameters += $", {{ \"{param.Name}\", {param.Name} }}";
                        }
                    }

                    AllParameters += $" }}";
                }

                if (returnType == "void")
                {
                    callMethod = $"{nameof(BaseContractProxy.CallAsync)}({stringMethodName}{AllParameters}).Wait();";
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    var generic = ExtractTaskGenericArgumentRegex(returnType);
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

                sb.AppendLine($"{baseIndent}        {callMethod}");
                sb.AppendLine($"{baseIndent}    }}");
            }

            sb.AppendLine($"{baseIndent}}}");
            sb.AppendLine("");

            var preserver = GenerateProxyPreserverClass(iface);
            sb.AppendLine(preserver);

            // Cerramos el namespace si lo abrimos
            if (hasNamespace)
            {
                sb.AppendLine($"}}");
            }

            return sb.ToString();
        }

        private static string ExtractTaskGenericArgumentRegex(string taskType)
        {
            // Patrón que captura todo entre el primer < y el último > balanceado
            var pattern = @"System\.Threading\.Tasks\.Task<(.+)>$";
            var match = Regex.Match(taskType, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "object";
        }

        private static string GenerateProxyPreserverClass(INamedTypeSymbol iface)
        {
            var sb = new StringBuilder();
            var proxyName = iface.Name + "Proxy";
            var namespaceName = iface.ContainingNamespace?.ToDisplayString();
            var hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            var baseIndent = hasNamespace ? "    " : "";

            // El PreserverModule también va en el mismo namespace con la indentación correcta
            sb.AppendLine($"{baseIndent}[EditorBrowsable(EditorBrowsableState.Never)]");
            sb.AppendLine($"{baseIndent}public static class {proxyName}PreserverModule");
            sb.AppendLine($"{baseIndent}{{");
            sb.AppendLine($"{baseIndent}    [ModuleInitializer]");
            sb.AppendLine($"{baseIndent}    public static void Init()");
            sb.AppendLine($"{baseIndent}    {{");

            // Si está en un namespace, necesitamos el nombre completo
            var fullProxyName = hasNamespace
                ? $"{namespaceName}.{proxyName}"
                : proxyName;

            sb.AppendLine($"{baseIndent}        _ = typeof({fullProxyName});");
            sb.AppendLine($"{baseIndent}    }}");
            sb.AppendLine($"{baseIndent}");
            sb.AppendLine($"{baseIndent}    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({fullProxyName}))]");
            sb.AppendLine($"{baseIndent}    public static void {proxyName}Preserver() {{ }}");
            sb.AppendLine($"{baseIndent}}}");

            return sb.ToString();
        }
    }
}