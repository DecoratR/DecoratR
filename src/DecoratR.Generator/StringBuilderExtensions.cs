using System.Text;

namespace DecoratR.Generator;

internal static class StringBuilderExtensions
{
    private const string GlobalPrefix = "global::";

    internal static StringBuilder AppendIndentedLine(this StringBuilder sb, int indent, string text)
    {
        sb.Append(' ', indent * 4);
        sb.AppendLine(text);
        return sb;
    }

    internal static StringBuilder AppendIndentedLine(this StringBuilder sb, int indent)
    {
        sb.AppendLine();
        return sb;
    }

    internal static StringBuilder AppendStrippedGlobalPrefix(this StringBuilder sb, string typeName)
    {
        if (typeName.StartsWith(GlobalPrefix, StringComparison.OrdinalIgnoreCase))
            sb.Append(typeName, GlobalPrefix.Length, typeName.Length - GlobalPrefix.Length);
        else
            sb.Append(typeName);

        return sb;
    }
}