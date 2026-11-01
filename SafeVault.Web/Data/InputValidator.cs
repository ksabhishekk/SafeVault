using System.Text.RegularExpressions;

namespace SafeVault.Web.Data;

public static partial class InputValidator
{
    public static bool IsValidUsername(string? username) =>
        username is not null &&
        !ContainsDisallowedMarkupOrSql(username) &&
        UsernamePattern().IsMatch(username);

    public static bool IsValidEmail(string? email) =>
        email is not null &&
        !ContainsDisallowedMarkupOrSql(email) &&
        !email.Contains("..", StringComparison.Ordinal) &&
        EmailPattern().IsMatch(email);

    private static bool ContainsDisallowedMarkupOrSql(string value) =>
        value.IndexOfAny(['<', '>', '\'', '"', ';', '/', '\\', '*', '=', '%']) >= 0 ||
        value.Contains("--", StringComparison.Ordinal) ||
        value.Contains("/*", StringComparison.Ordinal) ||
        value.Contains("*/", StringComparison.Ordinal);

    [GeneratedRegex(@"\A[A-Za-z0-9_]{3,30}\z", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9._+-]{0,62}[A-Za-z0-9_+-])?@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+\z", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
