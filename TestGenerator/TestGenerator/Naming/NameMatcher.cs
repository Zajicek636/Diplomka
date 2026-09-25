using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TestGenerator.Naming;

public static class NameMatcher
{
    public const double ExactScore = 1.0;
    public const double TokenScore = 0.8;

    private static readonly Regex PascalSplit = new(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);
    private static readonly Regex NonAlnum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IReadOnlyList<string> TokenizeAnalysisName(string name)
    {
        var plain = RemoveDiacritics(name).ToLowerInvariant();
        return NonAlnum.Split(plain).Where(t => t.Length > 0).ToList();
    }

    public static IReadOnlyList<string> TokenizeCodeName(string name)
    {
        var plain = RemoveDiacritics(name).Replace("_", " ");
        return PascalSplit.Replace(plain, " ")
            .ToLowerInvariant()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    public static double Score(string analysisName, string codeName)
    {
        var analysisTokens = TokenizeAnalysisName(analysisName);
        var codeTokens = TokenizeCodeName(codeName);
        if (analysisTokens.Count == 0 || codeTokens.Count == 0)
        {
            return 0;
        }

        if (string.Concat(analysisTokens) == string.Concat(codeTokens))
        {
            return ExactScore;
        }

        if (analysisTokens.Count != codeTokens.Count)
        {
            return 0;
        }

        var remaining = codeTokens.ToList();
        foreach (var token in analysisTokens)
        {
            var hit = remaining.FindIndex(c => StemEquals(token, c));
            if (hit < 0)
            {
                return 0;
            }
            remaining.RemoveAt(hit);
        }
        return TokenScore;
    }

    public static string ToPascalCase(string analysisName)
    {
        return string.Concat(TokenizeAnalysisName(analysisName)
            .Select(t => char.ToUpperInvariant(t[0]) + t.Substring(1)));
    }

    public static bool StemEquals(string a, string b)
    {
        if (a == b)
        {
            return true;
        }

        var shorter = Math.Min(a.Length, b.Length);
        var prefix = 0;
        while (prefix < shorter && a[prefix] == b[prefix])
        {
            prefix++;
        }

        if (prefix == shorter && shorter >= 3)
        {
            return true;
        }
        return shorter >= 4 && prefix >= Math.Max(4, shorter - 2);
    }

    public static IReadOnlyList<T> BestMatches<T>(string analysisName, IEnumerable<T> candidates, Func<T, string> codeName)
    {
        var scored = candidates
            .Select(c => (Candidate: c, Score: Score(analysisName, codeName(c))))
            .Where(x => x.Score > 0)
            .ToList();
        if (scored.Count == 0)
        {
            return Array.Empty<T>();
        }
        var best = scored.Max(x => x.Score);
        return scored.Where(x => x.Score == best).Select(x => x.Candidate).ToList();
    }
}
