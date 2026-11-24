using System.Text.RegularExpressions;

namespace PromptEnhancer.Core;

public sealed record Redaction(string Text, IReadOnlyList<string> Categories);

public static class Privacy
{
    public const int MaxDraft = 32000;
    private static Regex Rule(string expression) => new(expression,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));

    private static readonly (string Label, Regex Pattern, string Replacement)[] Patterns =
    [
        ("private key", Rule(@"(?s)-----BEGIN [A-Z ]*PRIVATE KEY-----.*?(?:-----END [A-Z ]*PRIVATE KEY-----|\z)"), "[REDACTED]"),
        ("authorization credential", Rule(@"(?i)(\b(?:Bearer|Basic)[ \t]+)[A-Za-z0-9/+_.=~\-]{6,}"), "$1[REDACTED]"),
        ("credential assignment", Rule("""(?i)(["']?\b(?:[a-z0-9_]*(?:api[_-]?key|token|secret|password|passwd|credential|connectionstring|database_url)[a-z0-9_]*|pwd)\b["']?\s*[:=]\s*)(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;}]+)"""), "$1[REDACTED]"),
        ("environment value", Rule("""(?m)^([ \t]*(?:export[ \t]+)?[A-Z_][A-Z0-9_]*[ \t]*=[ \t]*)[^\r\n]+"""), "$1[REDACTED]"),
        ("access token", Rule(@"\b(?:sk-[A-Za-z0-9_\-]{12,}|gh[pousr]_[A-Za-z0-9_]{12,}|github_pat_[A-Za-z0-9_]{12,}|AKIA[A-Z0-9]{16}|AIza[A-Za-z0-9_\-]{30,}|xox[baprs]-[A-Za-z0-9\-]{10,}|eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+)\b"), "[REDACTED]"),
        ("URL credentials", Rule(@"(?i)(\b[a-z][a-z0-9+.-]*://)[^\s/@]+:[^\s/@]+@"), "$1[REDACTED]@")
    ];

    public static Redaction Clean(string text)
    {
        if (text.Length > MaxDraft * 2) throw new ArgumentException("Text exceeds the supported length.");
        var categories = new List<string>();
        foreach (var (name, pattern, replacement) in Patterns)
        {
            var clean = pattern.Replace(text, replacement);
            if (clean != text) categories.Add(name);
            text = clean;
        }
        return new(text, categories);
    }
}

