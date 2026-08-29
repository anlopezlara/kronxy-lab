using System.Globalization;
using System.Text;

namespace Kronxy.Context.Packaging;

internal static class PackagePath
{
    public const string ManifestName = "manifest.json";
    private const int MaxPortableSegmentBytes = 255;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryCanonicalize(string? path, int maxUtf8Bytes, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrEmpty(path) || maxUtf8Bytes <= 0 || path != path.Trim() ||
            path.Contains('\\') || path.StartsWith('/') || path.EndsWith('/') ||
            path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') return false;

        try
        {
            _ = StrictUtf8.GetByteCount(path);
            if (path.EnumerateRunes().Any(IsUnsafeUnicodeRune)) return false;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or "..")) return false;
        try
        {
            var normalized = new string[segments.Length];
            for (var index = 0; index < segments.Length; index++)
            {
                normalized[index] = segments[index].Normalize(NormalizationForm.FormC);
                if (IsInvalidPortableSegment(normalized[index]) ||
                    StrictUtf8.GetByteCount(normalized[index]) > MaxPortableSegmentBytes) return false;
            }
            canonical = string.Join('/', normalized);
            return !canonical.Equals(ManifestName, StringComparison.OrdinalIgnoreCase) &&
                StrictUtf8.GetByteCount(canonical) <= maxUtf8Bytes;
        }
        catch (Exception exception) when (exception is ArgumentException or EncoderFallbackException)
        {
            canonical = string.Empty;
            return false;
        }
    }

    private static bool IsUnsafeUnicodeRune(Rune rune) => rune.Value == 0 ||
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or
            UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static bool IsInvalidPortableSegment(string segment)
    {
        if (segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0 || segment.EndsWith(' ') || segment.EndsWith('.')) return true;
        var baseName = segment.Split('.')[0].TrimEnd(' ', '.');
        return baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) || baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) || baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            baseName.Length == 4 && (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
            baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && baseName[3] is >= '1' and <= '9';
    }
}
