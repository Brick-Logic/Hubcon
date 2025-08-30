using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Hubcon.Analyzers.DiagnosticSuppressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MinimalSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor SuppressDescriptor =
            new SuppressionDescriptor("SUPTEST", "CS8974", "Justificación");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
            ImmutableArray.Create(SuppressDescriptor);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            // No hace nada
        }
    }
}
