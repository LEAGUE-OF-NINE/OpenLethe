namespace OpenLethe.Tests.Replay;

public static class MaskMatch
{
    // Glob where a literal "[*]" segment matches any "[<int>]". Everything else literal.
    public static bool Matches(string pattern, string path)
    {
        // Split on [*] and escape each part, then rejoin with digit matcher including brackets
        var parts = pattern.Split("[*]");
        var regexParts = parts.Select(p => System.Text.RegularExpressions.Regex.Escape(p)).ToList();
        var regexPattern = "^" + string.Join("\\[[0-9]+\\]", regexParts) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern);
    }
}
