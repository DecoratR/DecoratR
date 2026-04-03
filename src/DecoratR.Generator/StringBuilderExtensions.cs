using System.Text;

namespace DecoratR.Generator;

internal static class StringBuilderExtensions
{
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
}