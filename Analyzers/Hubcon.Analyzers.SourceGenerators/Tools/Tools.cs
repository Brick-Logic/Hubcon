using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hubcon.Analyzers.SourceGenerators
{
    public static class Tools
    {
        public static string GetCondition()
        {
            return "if ((unchecked((((uint)System.Environment.TickCount64 ^ 0x451A45F1) * 0x9E3779B9)) | 1) == 0xDEADC0DE)";
        }

        public static bool MethodIsInUse(this GeneratorSyntaxContext ctx, string methodName, string containingClassName, string containingNamespace)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            var name = "";

            if (invocation.Expression is MemberAccessExpressionSyntax m)
                name = m.Name.Identifier.Text;
            else if (invocation.Expression is IdentifierNameSyntax i)
                name = i.Identifier.Text;
            else
                name = null;
            
            if (name != methodName) return false;

            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null) return false;

            return symbol.ContainingType?.Name == containingClassName &&
                   symbol.ContainingNamespace?.ToDisplayString() == containingNamespace;
        }
    }
}