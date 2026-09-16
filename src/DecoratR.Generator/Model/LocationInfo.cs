using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DecoratR.Generator.Model;

/// <summary>
/// An equatable, cache-friendly stand-in for <see cref="Location"/>.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location? location)
    {
        if (location is null || !location.IsInSource || location.SourceTree is null) return null;

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public static LocationInfo? From(SyntaxNode? node) => node is null ? null : From(node.GetLocation());

    public static LocationInfo? From(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            var info = From(location);
            if (info is not null) return info;
        }

        return null;
    }
}
