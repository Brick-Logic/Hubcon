using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HubconAnalyzer
{
    using System.Composition;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Formatting;
    using Microsoft.CodeAnalysis.CodeActions;
    using System.Threading;
    using System.Linq;

    namespace HubconAnalyzer.CodeFixes
    {
        [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HubconInjectNullableCodeFix)), Shared]
        public class HubconInjectNullableCodeFix : CodeFixProvider
        {
            public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("HUBCON001");

            public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

            public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
            {
                var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                var diagnostic = context.Diagnostics.First();
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                var propertyDecl = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                    .OfType<PropertyDeclarationSyntax>().First();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Agregar SuppressMessage CS8618",
                        createChangedDocument: c => AddSuppressMessageAsync(context.Document, propertyDecl, c),
                        equivalenceKey: "Agregar SuppressMessage CS8618"),
                    diagnostic);
            }

            private async Task<Document> AddSuppressMessageAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
            {
                var suppressAttr = SyntaxFactory.Attribute(
                    SyntaxFactory.IdentifierName("System.Diagnostics.CodeAnalysis.SuppressMessage"),
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList<AttributeArgumentSyntax>(new[]
                        {
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("NullableWarning"))),

                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("CS8618")))
                        })
                    ));

                var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(suppressAttr))
                    .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.LineFeed));

                var newProperty = property.AddAttributeLists(attrList);
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var newRoot = root.ReplaceNode(property, newProperty);

                return document.WithSyntaxRoot(newRoot);
            }
        }
    }

}
