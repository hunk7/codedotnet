using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Analysis;

/// <summary>A rule hit from syntax-only analysis. These are leads for the model, not proof.</summary>
public sealed record Finding(string RuleId, string Severity, string Message, int Line, int Column);

public sealed record TypeFact(string Kind, string Name, int Line);

public sealed record MethodFact(string Name, string ReturnType, bool IsAsync, int Line, int StatementCount);

/// <summary>Everything the analyzer established about a source file, before any model is involved.</summary>
public sealed record CodeEvidence(
    bool Compiles,
    IReadOnlyList<DiagnosticInfo> Diagnostics,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<TypeFact> Types,
    IReadOnlyList<MethodFact> Methods,
    IReadOnlyList<string> Usings,
    int LineCount)
{
    /// <summary>Lines worth showing the model: errors and warnings first, then findings.</summary>
    public IReadOnlyList<int> InterestingLines() =>
        Diagnostics.Where(d => d.Severity is "Error" or "Warning").Select(d => d.StartLine)
            .Concat(Findings.Select(f => f.Line))
            .Where(l => l > 0)
            .Distinct()
            .ToList();
}
