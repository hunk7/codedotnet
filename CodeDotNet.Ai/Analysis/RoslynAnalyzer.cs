using CodeDotNet.Contracts;
using CodeDotNet.Engine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeDotNet.Ai.Analysis;

/// <summary>
/// Roslyn-first evidence gathering (ADR-012). Diagnostics come from the same
/// <see cref="CompilerHost"/> the sandbox and browser use, so they match by construction.
/// Rules here are syntax-only and deliberately few; each states what it does not prove.
/// Source is parsed and compiled, never executed.
/// </summary>
public static class RoslynAnalyzer
{
    public const int MaxSourceChars = 64_000;

    public static CodeEvidence Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var compile = CompilerHost.Compile(source);

        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Regular));
        var root = tree.GetCompilationUnitRoot();

        var usings = root.Usings.Select(u => u.Name?.ToString() ?? "").Where(n => n.Length > 0).ToList();
        var types = new List<TypeFact>();
        var methods = new List<MethodFact>();
        var findings = new List<Finding>();

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax t:
                    types.Add(new TypeFact(t.Kind().ToString().Replace("Declaration", ""), t.Identifier.Text, Line(t)));
                    break;
                case MethodDeclarationSyntax m:
                    methods.Add(ToMethodFact(m));
                    CheckAsyncVoid(m, findings);
                    CheckInfiniteLoops(m, findings);
                    break;
                case LocalFunctionStatementSyntax lf:
                    methods.Add(new MethodFact(lf.Identifier.Text, lf.ReturnType.ToString(),
                        lf.Modifiers.Any(SyntaxKind.AsyncKeyword), Line(lf), CountStatements(lf.Body)));
                    break;
                case CatchClauseSyntax c:
                    CheckEmptyCatch(c, findings);
                    break;
                case MemberAccessExpressionSyntax ma:
                    CheckBlocking(ma, findings);
                    break;
            }
        }

        // Top-level programs have loops outside any method declaration.
        foreach (var global in root.Members.OfType<GlobalStatementSyntax>())
        {
            CheckInfiniteLoops(global, findings);
        }

        return new CodeEvidence(
            compile.Success,
            compile.Diagnostics,
            findings.OrderBy(f => f.Line).ThenBy(f => f.RuleId).ToList(),
            types,
            methods,
            usings,
            source.Length == 0 ? 0 : source.Count(c => c == '\n') + 1);
    }

    private static MethodFact ToMethodFact(MethodDeclarationSyntax m) =>
        new(m.Identifier.Text, m.ReturnType.ToString(), m.Modifiers.Any(SyntaxKind.AsyncKeyword), Line(m),
            CountStatements(m.Body) + (m.ExpressionBody is null ? 0 : 1));

    private static int CountStatements(BlockSyntax? body) =>
        body?.DescendantNodes().OfType<StatementSyntax>().Count(s => s is not BlockSyntax) ?? 0;

    private static void CheckAsyncVoid(MethodDeclarationSyntax m, List<Finding> findings)
    {
        if (!m.Modifiers.Any(SyntaxKind.AsyncKeyword) || m.ReturnType is not PredefinedTypeSyntax p
            || !p.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return;
        }

        // Event handlers legitimately use async void: (object sender, EventArgs e).
        var parameters = m.ParameterList.Parameters;
        if (parameters.Count == 2 && parameters[0].Type?.ToString() == "object")
        {
            return;
        }

        findings.Add(new Finding("CDN001", "Warning",
            $"'{m.Identifier.Text}' is 'async void'; exceptions cannot be awaited or caught by the caller. Consider 'async Task'.",
            Line(m.Identifier), Column(m.Identifier)));
    }

    private static void CheckBlocking(MemberAccessExpressionSyntax ma, List<Finding> findings)
    {
        var name = ma.Name.Identifier.Text;
        var isBlocking = name switch
        {
            "Result" => ma.Parent is not InvocationExpressionSyntax,
            "Wait" or "GetResult" => ma.Parent is InvocationExpressionSyntax,
            _ => false,
        };

        if (!isBlocking)
        {
            return;
        }

        findings.Add(new Finding("CDN002", "Info",
            $"Possible blocking wait on a task via '.{name}'. This can deadlock or hide exceptions; prefer 'await'. " +
            "Syntax-only: the receiver may not be a Task.",
            Line(ma.Name), Column(ma.Name)));
    }

    private static void CheckEmptyCatch(CatchClauseSyntax c, List<Finding> findings)
    {
        if (c.Block.Statements.Count > 0)
        {
            return;
        }

        findings.Add(new Finding("CDN003", "Warning",
            "Empty catch block silently swallows the exception.",
            Line(c), Column(c.CatchKeyword)));
    }

    private static void CheckInfiniteLoops(SyntaxNode scope, List<Finding> findings)
    {
        foreach (var loop in scope.DescendantNodes().Where(n => n is WhileStatementSyntax or ForStatementSyntax or DoStatementSyntax))
        {
            var isConstantTrue = loop switch
            {
                WhileStatementSyntax w => w.Condition.IsKind(SyntaxKind.TrueLiteralExpression),
                DoStatementSyntax d => d.Condition.IsKind(SyntaxKind.TrueLiteralExpression),
                ForStatementSyntax f => f.Condition is null || f.Condition.IsKind(SyntaxKind.TrueLiteralExpression),
                _ => false,
            };

            if (!isConstantTrue || HasExit(loop))
            {
                continue;
            }

            findings.Add(new Finding("CDN004", "Warning",
                "Loop condition is constant true and no 'break', 'return', 'throw' or 'goto' was found inside it. " +
                "It may never terminate (the sandbox would stop it at the timeout).",
                Line(loop), Column(loop)));
        }
    }

    private static bool HasExit(SyntaxNode loop)
    {
        var body = loop switch
        {
            WhileStatementSyntax w => w.Statement,
            DoStatementSyntax d => d.Statement,
            ForStatementSyntax f => f.Statement,
            _ => loop,
        };

        return body.DescendantNodesAndSelf(n => n is not (LambdaExpressionSyntax or AnonymousMethodExpressionSyntax or LocalFunctionStatementSyntax))
            .Any(n => n is BreakStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax or GotoStatementSyntax
                || n is ThrowExpressionSyntax);
    }

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static int Line(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static int Column(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Character + 1;

    private static int Column(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Character + 1;
}
