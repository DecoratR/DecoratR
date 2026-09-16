using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace DecoratR.Generator;

/// <summary>
/// Turns arbitrary strings (assembly names, type names) into valid C# identifiers and namespaces.
/// </summary>
internal static class IdentifierSanitizer
{
    /// <summary>
    /// Converts an assembly name such as <c>My-App.Web</c> into a valid namespace (<c>My_App.Web</c>).
    /// Segments that are C# keywords are escaped with <c>@</c>.
    /// </summary>
    public static string ToNamespace(string name)
    {
        var segments = name.Split('.');
        var sb = new StringBuilder(name.Length + 4);

        for (var i = 0; i < segments.Length; i++)
        {
            if (i > 0) sb.Append('.');
            sb.Append(ToIdentifier(segments[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a single segment into a valid identifier: invalid characters become <c>_</c>, a leading digit
    /// is prefixed with <c>_</c>, keywords are escaped with <c>@</c>.
    /// </summary>
    public static string ToIdentifier(string segment)
    {
        var sb = new StringBuilder(segment.Length + 1);

        foreach (var ch in segment)
        {
            if (sb.Length == 0)
            {
                if (SyntaxFacts.IsIdentifierStartCharacter(ch))
                {
                    sb.Append(ch);
                    continue;
                }

                sb.Append('_');
                if (SyntaxFacts.IsIdentifierPartCharacter(ch)) sb.Append(ch);
                continue;
            }

            sb.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');
        }

        if (sb.Length == 0) sb.Append('_');

        var identifier = sb.ToString();
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;
    }

    /// <summary>
    /// Builds a method-name-safe identifier from a fully qualified type name, e.g.
    /// <c>global::App.Logging.LoggingDecorator</c> becomes <c>App_Logging_LoggingDecorator</c>.
    /// </summary>
    public static string FromTypeName(string fullyQualifiedName)
    {
        var name = fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal)
            ? fullyQualifiedName.Substring("global::".Length)
            : fullyQualifiedName;

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');

        return ToIdentifier(sb.ToString()).TrimStart('@');
    }
}
