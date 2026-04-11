namespace DecoratR.Generator.Tests.Infrastructure;

public static class GeneratedOutputExtensions
{
    public static string FindSource(this string[] generatedSources, string marker)
    {
        var match = generatedSources.FirstOrDefault(t => t.Contains(marker));
        if (match is not null)
            return match;

        var available = generatedSources
            .Select((s, i) => $"  [{i}] {GetFirstMeaningfulType(s)}")
            .ToArray();

        throw new InvalidOperationException(
            $"No generated source contains marker '{marker}'. " +
            $"Generated {generatedSources.Length} sources:\n{string.Join("\n", available)}");
    }

    public static string GetDecoratorApplicationSection(this string source)
    {
        const string startMarker = "// Apply decorators";
        const string endMarker = "return services;";

        var startIdx = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIdx < 0)
            throw new InvalidOperationException(
                $"Generated source does not contain '{startMarker}'.");

        var endIdx = source.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
        if (endIdx < 0)
            throw new InvalidOperationException(
                $"Generated source does not contain '{endMarker}' after '{startMarker}'.");

        return source[startIdx..endIdx];
    }

    public static string GetSectionBetween(this string source, string startMarker, string endMarker)
    {
        var startIdx = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIdx < 0)
            throw new InvalidOperationException(
                $"Source does not contain start marker '{startMarker}'.");

        var searchFrom = startIdx + startMarker.Length;
        var endIdx = source.IndexOf(endMarker, searchFrom, StringComparison.Ordinal);
        if (endIdx < 0)
            throw new InvalidOperationException(
                $"Source does not contain end marker '{endMarker}' after '{startMarker}'.");

        return source[startIdx..(endIdx + endMarker.Length)];
    }

    public static int CountOccurrences(this string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static string GetFirstMeaningfulType(string source)
    {
        var classIdx = source.IndexOf("class ", StringComparison.Ordinal);
        if (classIdx < 0)
            return source[..Math.Min(80, source.Length)].Replace("\n", " ");

        var lineEnd = source.IndexOf('\n', classIdx);
        if (lineEnd < 0) lineEnd = source.Length;
        return source[classIdx..Math.Min(lineEnd, classIdx + 80)].Trim();
    }
}
