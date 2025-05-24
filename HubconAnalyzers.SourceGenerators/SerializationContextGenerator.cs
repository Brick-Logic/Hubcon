using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HubconAnalyzers.SourceGenerators
{
    //[Generator]
    public class SerializationContextGenerator : IIncrementalGenerator
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
                .Where(symbol => symbol != null)
                .Collect();

            context.RegisterSourceOutput(interfaces, (spc, interfaceList) =>
            {
                var filename = $"SerializationContext.g.cs";
                var code = GenerateSerializationContextClass(interfaceList);
                spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
            });
        }

        private static string GenerateSerializationContextClass(ImmutableArray<INamedTypeSymbol> interfaces)
        {
            var sb = new StringBuilder();
            List<string> usings = new List<string>();
            List<string> serializerAttributes = new List<string>();
            List<string> typeDeclarations = new List<string>();

            foreach(var iface in interfaces)
            {
                usings.Add($"using {iface.ContainingNamespace.ToString()};");

                foreach (var method in iface.GetMembers().OfType<IMethodSymbol>())
                {
                    if (!usings.Any(x => x == method.ReturnType.ContainingNamespace.ToString()))
                    {
                        usings.Add($"using {method.ReturnType.ContainingNamespace.ToString()};");
                    }

                    if(!serializerAttributes.Any(x => x == method.ReturnType.ToString().TrimEnd('?')))
                    {
                        serializerAttributes.Add($"[JsonSerializable(typeof({method.ReturnType.ToString().TrimEnd('?')}))]");
                    }

                    if(!typeDeclarations.Any(x => x.Contains(method.ReturnType.ToString().TrimEnd('?'))))
                    {
                        typeDeclarations.Add($"{{ typeof({method.ReturnType.ToString().TrimEnd('?')}), SerializationContext.Default.{method.ReturnType.Name} }}");
                    }

                    foreach(var parameter in method.Parameters)
                    {
                        if (!serializerAttributes.Any(x => x == parameter.Type.ToString().TrimEnd('?')))
                        {
                            serializerAttributes.Add($"[JsonSerializable(typeof({parameter.Type.ToString().TrimEnd('?')}))]");
                        }

                        if (!usings.Contains($"{parameter.Type.ContainingNamespace}"))
                        {
                            if(!(parameter.Type.ContainingNamespace == null))
                                usings.Add($"using {parameter.Type.ContainingNamespace};");
                        }

                        if (!typeDeclarations.Any(x => x.Contains(parameter.Type.ToString())))
                        {
                            typeDeclarations.Add($"            {{ typeof({parameter.Type.ToString().TrimEnd('?')}), SerializationContext.Default.{parameter.Type.Name} }}");
                        }
                    }
                }
            }

            sb.AppendLine($"using System.Text.Json;");
            sb.AppendLine($"using System.Text.Json.Serialization;");
            sb.AppendLine($"using System.Text.Json.Serialization.Metadata;");

            foreach(var item in usings.Distinct())
            {
                sb.AppendLine(item);
            }

            sb.AppendLine($"");
            sb.AppendLine($"namespace Hubcon.Shared.Core.Serialization;");
            sb.AppendLine($"{{");

            foreach(var item in serializerAttributes.Distinct())
            {
                sb.AppendLine(item);
            }

            sb.AppendLine($"");
            sb.AppendLine($"    public partial class SerializationContext : JsonSerializerContext");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        public static readonly Dictionary<Type, JsonTypeInfo> TypeMappings = new()");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            {{ typeof(bool), PrimitiveJsonContext.Default.Boolean }},");
            sb.AppendLine($"            {{ typeof(byte), PrimitiveJsonContext.Default.Byte }},");
            sb.AppendLine($"            {{ typeof(sbyte), PrimitiveJsonContext.Default.SByte }},");
            sb.AppendLine($"            {{ typeof(char), PrimitiveJsonContext.Default.Char }},");
            sb.AppendLine($"            {{ typeof(decimal), PrimitiveJsonContext.Default.Decimal }},");
            sb.AppendLine($"            {{ typeof(double), PrimitiveJsonContext.Default.Double }},");
            sb.AppendLine($"            {{ typeof(float), PrimitiveJsonContext.Default.Single }},");
            sb.AppendLine($"            {{ typeof(int), PrimitiveJsonContext.Default.Int32 }},");
            sb.AppendLine($"            {{ typeof(uint), PrimitiveJsonContext.Default.UInt32 }},");
            sb.AppendLine($"            {{ typeof(nint), PrimitiveJsonContext.Default.IntPtr }},");
            sb.AppendLine($"            {{ typeof(nuint), PrimitiveJsonContext.Default.UIntPtr }},");
            sb.AppendLine($"            {{ typeof(long), PrimitiveJsonContext.Default.Int64 }},");
            sb.AppendLine($"            {{ typeof(ulong), PrimitiveJsonContext.Default.UInt64 }},");
            sb.AppendLine($"            {{ typeof(short), PrimitiveJsonContext.Default.Int16 }},");
            sb.AppendLine($"            {{ typeof(ushort), PrimitiveJsonContext.Default.UInt16 }},");
            sb.AppendLine($"            {{ typeof(string), PrimitiveJsonContext.Default.String }},");
            sb.AppendLine($"            {{ typeof(object), PrimitiveJsonContext.Default.Object }},");
            sb.AppendLine($"            {{ typeof(JsonElement), PrimitiveJsonContext.Default.Object }},");
            
            foreach(var item in typeDeclarations.Distinct())
            {
                sb.AppendLine($"            {item}");
            }

            sb.AppendLine(string.Join(",\n", typeDeclarations.Distinct()));                  
            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");

            return sb.ToString();
        }
    }
}