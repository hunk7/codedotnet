using System.Text;

namespace CodeDotNet.Ai.Analysis;

/// <summary>
/// Fits source into a model context budget. Short files go in whole. Long files include
/// windows around the lines the analyzer flagged, with explicit gap markers, and the caller
/// is told the source was truncated so the model and the user are not misled.
/// </summary>
public static class SourceWindow
{
    public const int CharsPerToken = 3;
    public const int PromptOverheadTokens = 900;
    public const int ContextLines = 12;

    public static int MaxSourceChars(int numCtx, int numPredict) =>
        Math.Max(2000, (numCtx - numPredict - PromptOverheadTokens) * CharsPerToken);

    public static (string Text, bool Truncated) Build(string source, IReadOnlyList<int> interestingLines, int maxChars)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var width = lines.Length.ToString().Length;
        string Numbered(int i) => $"{(i + 1).ToString().PadLeft(width)}: {lines[i]}";

        var whole = string.Join('\n', Enumerable.Range(0, lines.Length).Select(Numbered));
        if (whole.Length <= maxChars)
        {
            return (whole, false);
        }

        var keep = new SortedSet<int>();
        var sb = new StringBuilder();
        var used = 0;

        bool TryAdd(int i)
        {
            if (keep.Contains(i))
            {
                return true;
            }

            var cost = Numbered(i).Length + 1;
            if (used + cost > maxChars)
            {
                return false;
            }

            used += cost;
            keep.Add(i);
            return true;
        }

        foreach (var line in interestingLines)
        {
            var center = Math.Clamp(line - 1, 0, lines.Length - 1);
            for (var offset = 0; offset <= ContextLines; offset++)
            {
                foreach (var i in new[] { center - offset, center + offset })
                {
                    if (i >= 0 && i < lines.Length)
                    {
                        TryAdd(i);
                    }
                }
            }
        }

        for (var i = 0; i < lines.Length && TryAdd(i); i++)
        {
        }

        var previous = -1;
        foreach (var i in keep)
        {
            if (previous >= 0 && i != previous + 1)
            {
                sb.Append("   ... (").Append(i - previous - 1).Append(" lines omitted)\n");
            }

            sb.Append(Numbered(i)).Append('\n');
            previous = i;
        }

        if (previous < lines.Length - 1)
        {
            sb.Append("   ... (").Append(lines.Length - 1 - previous).Append(" lines omitted)\n");
        }

        return (sb.ToString().TrimEnd('\n'), true);
    }
}
