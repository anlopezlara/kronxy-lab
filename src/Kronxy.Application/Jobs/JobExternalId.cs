namespace Kronxy.Application.Jobs;

public static class JobExternalId
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (!trimmed.StartsWith(
                "KRX-",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = trimmed.AsSpan(4);

        return number.Length is >= 6 and <= 18 &&
               number.ToArray().All(char.IsAsciiDigit);
    }
}
