using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Not.Analyzers.Base;

public abstract class AnalyzerBase : DiagnosticAnalyzer
{
    private readonly DiagnosticDescriptor _errorRule;
    private readonly DiagnosticDescriptor _rule;

    public string DiagnosticId { get; }

    protected AnalyzerBase(
        string diagnosticId,
        string title,
        string messageFormat,
        string category = "Style",
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        string? description = null
    )
    {
        DiagnosticId = diagnosticId;
        _errorRule = new(
            "NA0000",
            "Internal analyzer error",
            "An error occurred in analyzer: {0}",
            "Debug",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
        _rule = new DiagnosticDescriptor(
            diagnosticId,
            title,
            messageFormat,
            category,
            severity,
            isEnabledByDefault: true,
            description: description
        );
    }

    protected abstract void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create([_errorRule, _rule]);

    protected void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        try
        {
            SafeAnalyzeSyntaxNode(context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Analyzer error: {ex}");

            var diagnostic = Diagnostic.Create(_errorRule, context.Node?.GetLocation(), ex.ToString());
            context.ReportDiagnostic(diagnostic);
        }
    }

    protected Diagnostic CreateDiagnostic(Location location, params object[] messageArgs) =>
        Diagnostic.Create(_rule, location, messageArgs);
}
