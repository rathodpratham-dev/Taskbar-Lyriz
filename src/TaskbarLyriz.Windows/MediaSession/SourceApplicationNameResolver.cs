using System.Text.RegularExpressions;

namespace TaskbarLyriz.Windows.MediaSession;

internal static partial class SourceApplicationNameResolver
{
    internal static string Resolve(string? sourceApplicationId)
    {
        if (string.IsNullOrWhiteSpace(sourceApplicationId))
        {
            return "Unknown player";
        }

        var identifier = sourceApplicationId.Trim();
        var appIdSeparator = identifier.LastIndexOf('!');
        if (appIdSeparator >= 0 && appIdSeparator < identifier.Length - 1)
        {
            identifier = identifier[(appIdSeparator + 1)..];
        }

        identifier = Path.GetFileName(identifier);
        if (identifier.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            identifier = identifier[..^4];
        }
        var packageSeparator = identifier.IndexOf('_');
        if (packageSeparator > 0)
        {
            identifier = identifier[..packageSeparator];
        }

        var segments = identifier
            .Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        identifier = segments.Length > 0 ? segments[^1] : identifier;
        identifier = PascalCaseBoundary().Replace(identifier, "$1 $2").Trim();

        if (identifier.Length == 0)
        {
            return "Unknown player";
        }

        return char.ToUpperInvariant(identifier[0]) + identifier[1..];
    }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCaseBoundary();
}
