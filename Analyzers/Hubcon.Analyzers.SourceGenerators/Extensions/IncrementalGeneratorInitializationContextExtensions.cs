using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hubcon.Analyzers.SourceGenerators.Extensions
{
    public static class IncrementalGeneratorInitializationContextExtensions
    {
        public static IncrementalValueProvider<bool> GetHubconClientProvider(this IncrementalGeneratorInitializationContext context)
        {
            return context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => node is InvocationExpressionSyntax,
                    transform: (ctx, _) => 
                        ctx.MethodIsInUse("AddHubconClient", "DependencyInjection", "Hubcon"))
                .Where(found => found)
                .Collect()
                .Select((calls, _) => calls.Any());
        }

        public static IncrementalValueProvider<bool> GetHubconServerProvider(this IncrementalGeneratorInitializationContext context)
        {
            return context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => node is InvocationExpressionSyntax,
                    transform: (ctx, _) => 
                        ctx.MethodIsInUse("AddHubconServer", "ServerDependencyInjection", "Hubcon"))
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