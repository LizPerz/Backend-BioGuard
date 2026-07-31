namespace BioGuard.Api.Services;

public static class InputSanitizer
{
    public static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;
        return System.Net.WebUtility.HtmlEncode(input);
    }
}
