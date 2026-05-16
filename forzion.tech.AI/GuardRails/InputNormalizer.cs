using System.Text;
using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public static class InputNormalizer
{
    private static readonly Regex TagChars = new(@"[0-F]", RegexOptions.Compiled);
    private static readonly Regex ZeroWidth = new(@"[​-‍﻿]", RegexOptions.Compiled);

    public static string NormalizeUnicode(string input)
    {
        var noTags = TagChars.Replace(input, "");
        var noZw = ZeroWidth.Replace(noTags, "");
        return noZw.Normalize(NormalizationForm.FormC);
    }

    public static int EstimateTokens(string text) => text.Length / 4;
}
