using System.Text.RegularExpressions;

namespace AiAgentSecurityScanner.Api.Services;

public interface IPromptNormalizer
{
    string Normalize(string prompt);
}

public partial class PromptNormalizer : IPromptNormalizer
{
    private static readonly Dictionary<string, string> NumberWords = new()
    {
        ["0"] = "zero", ["1"] = "one", ["2"] = "two", ["3"] = "three", ["4"] = "four",
        ["5"] = "five", ["6"] = "six", ["7"] = "seven", ["8"] = "eight", ["9"] = "nine",
        ["10"] = "ten", ["11"] = "eleven", ["12"] = "twelve", ["13"] = "thirteen",
        ["14"] = "fourteen", ["15"] = "fifteen", ["16"] = "sixteen", ["17"] = "seventeen",
        ["18"] = "eighteen", ["19"] = "nineteen", ["20"] = "twenty"
    };

    public string Normalize(string prompt)
    {
        prompt = prompt.Trim().ToLowerInvariant();
        prompt = NumberPattern().Replace(prompt, match =>
            NumberWords.TryGetValue(match.Value, out var word) ? word : match.Value);
        prompt = PunctuationPattern().Replace(prompt, " ");
        return WhitespacePattern().Replace(prompt, " ").Trim();
    }

    [GeneratedRegex(@"\b(?:20|1[0-9]|[0-9])\b")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
