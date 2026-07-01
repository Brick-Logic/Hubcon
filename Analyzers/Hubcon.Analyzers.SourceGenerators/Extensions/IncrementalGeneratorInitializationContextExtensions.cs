using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hubcon.Analyzers.SourceGenerators.Extensions
{
    public static class IncrementalGeneratorInitializationContextExtensions
    {
        public static IncrementalValueProvider<bool> GethubconProvider(this IncrementalGeneratorInitializationContext context)
        {
            return context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => node is InvocationExpressionSyntax,
                    transform: (ctx, _) =>
                    {
                        var invocation = (InvocationExpressionSyntax)ctx.Node;
                        var name = "";

                        if (invocation.Expression is MemberAccessExpressionSyntax m)
                            name = m.Name.Identifier.Text;
                        else if (invocation.Expression is IdentifierNameSyntax i)
                            name = i.Identifier.Text;
                        else
                            name = null;
                        
                        if (name != "AddHubconClient") return false;

                        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (symbol == null) return false;

                        return symbol.ContainingType?.Name == "DependencyInjection" &&
                               symbol.ContainingNamespace?.ToDisplayString() == "Hubcon";
                    })
                .Where(found => found)
                .Collect()
                .Select((calls, _) => calls.Any());
        }

        public static IncrementalValuesProvider<T> CreateNext<T>(
            this IncrementalGeneratorInitializationContext context, 
            Func<GeneratorSyntaxContext, CancellationToken, T> transformFunction)
        {
            return context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: (s, _) => s is InterfaceDeclarationSyntax,
                    transform: transformFunction);
        }
    }
}