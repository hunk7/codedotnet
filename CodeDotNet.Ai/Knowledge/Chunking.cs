using System.Text;
using System.Text.RegularExpressions;

namespace CodeDotNet.Ai.Knowledge;

/// <summary>Structure-aware Markdown/text chunker: headings define sections, code fences are kept whole where possible.</summary>
public static partial class MarkdownChunker
{
    public const int MaxChunkChars = 1200;

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingRx();

    public static IReadOnlyList<(string Heading, string Text)> Chunk(string text)
    {
        var blocks = new List<(string Heading, string Text)>();
        var stack = new List<(int Level, string Title)>();
        var cur = new StringBuilder();
        var inFence = false;

        string Heading() => string.Join(" > ", stack.Select(s => s.Title));

        void Flush()
        {
            var t = cur.ToString().TrimEnd();
            if (t.Trim().Length > 0)
            {
                blocks.Add((Heading(), t));
            }

            cur.Clear();
        }

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                if (!inFence)
                {
                    Flush();
                    inFence = true;
                    cur.AppendLine(line);
                }
                else
                {
                    cur.AppendLine(line);
                    inFence = false;
                    Flush();
                }

                continue;
            }

            if (inFence)
            {
                cur.AppendLine(line);
                continue;
            }

            var m = HeadingRx().Match(line);
            if (m.Success)
            {
                Flush();
                var level = m.Groups[1].Length;
                stack.RemoveAll(s => s.Level >= level);
                stack.Add((level, m.Groups[2].Value.Trim()));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
            }
            else
            {
                cur.AppendLine(line);
            }
        }

        Flush();
        return Pack(blocks);
    }

    private static List<(string Heading, string Text)> Pack(List<(string Heading, string Text)> blocks)
    {
        var result = new List<(string, string)>();
        var buf = new StringBuilder();
        var bufHeading = "";

        void Emit()
        {
            if (buf.Length > 0)
            {
                result.Add((bufHeading, buf.ToString()));
                buf.Clear();
            }
        }

        foreach (var (heading, text) in blocks)
        {
            if (heading != bufHeading)
            {
                Emit();
                bufHeading = heading;
            }

            foreach (var piece in text.Length > MaxChunkChars ? Split(text) : [text])
            {
                if (buf.Length > 0 && buf.Length + piece.Length + 2 > MaxChunkChars)
                {
                    Emit();
                }

                if (buf.Length > 0)
                {
                    buf.Append("\n\n");
                }

                buf.Append(piece);
            }
        }

        Emit();
        return result;
    }

    private static IEnumerable<string> Split(string text)
    {
        var sb = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var l = line;
            while (l.Length > MaxChunkChars)
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                yield return l[..MaxChunkChars];
                l = l[MaxChunkChars..];
            }

            if (sb.Length > 0 && sb.Length + l.Length + 1 > MaxChunkChars)
            {
                yield return sb.ToString();
                sb.Clear();
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(l);
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}

/// <summary>Reciprocal-rank fusion over several ranked id lists.</summary>
public static class RankFusion
{
    public const int DefaultK = 60;

    public static IReadOnlyList<(string Id, double Score)> Fuse(IEnumerable<IReadOnlyList<string>> rankings, int k = DefaultK)
    {
        var scores = new Dictionary<string, double>();
        foreach (var ranking in rankings)
        {
            for (var i = 0; i < ranking.Count; i++)
            {
                scores[ranking[i]] = scores.GetValueOrDefault(ranking[i]) + 1.0 / (k + i + 1);
            }
        }

        return scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
