using System.Text;
using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public static class InputNormalizer
{
    // Unicode Tag Block (U+E0000-U+E007F) as UTF-16 surrogate pairs: high=\uDB40, low=\uDC00-\uDC7F
    // These invisible characters are used in prompt injection attacks via hidden instructions.
    private static readonly Regex TagChars = new(@"\uDB40[\uDC00-\uDC7F]", RegexOptions.Compiled);

    // Zero-width chars: U+200B (ZW space), U+200C (ZW non-joiner), U+200D (ZW joiner), U+FEFF (BOM)
    private static readonly Regex ZeroWidth = new("[​-‍﻿]", RegexOptions.Compiled);

    public static string NormalizeUnicode(string input)
    {
        var noTags = TagChars.Replace(input, "");
        var noZw = ZeroWidth.Replace(noTags, "");
        return noZw.Normalize(NormalizationForm.FormC);
    }

    public static int EstimateTokens(string text) => text.Length / 4;
}
