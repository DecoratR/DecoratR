namespace DecoratR.Generator.Tests.Infrastructure;

public static class GeneratedOutputExtensions
{
    public const string RequestHandler = "global::DecoratR.IRequestHandler";
    public const string StreamRequestHandler = "global::DecoratR.IStreamRequestHandler";

    /// <summary>The whole decorator section of <c>AddDecoratR()</c> (all pipelines).</summary>
    public static string GetDecoratorSection(this string registrations) =>
        registrations.GetSectionBetween("// Decorator pipelines", "return services;");

    /// <summary>The <c>pipelines.Add(...)</c> entry for one service type, or <see langword="null"/> when none exists.</summary>
    public static string? TryGetPipeline(this string registrations, string requestType, string responseType, bool isStream = false)
    {
        var marker = $"pipelines.Add(typeof({(isStream ? StreamRequestHandler : RequestHandler)}<{requestType}, {responseType}>)";
        var start = registrations.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;

        var end = registrations.IndexOf("}));", start, StringComparison.Ordinal);
        return registrations[start..(end + 4)];
    }

    public static string GetPipeline(this string registrations, string requestType, string responseType, bool isStream = false) =>
        registrations.TryGetPipeline(requestType, responseType, isStream)
        ?? throw new InvalidOperationException($"No decorator pipeline for <{requestType}, {responseType}> in:{Environment.NewLine}{registrations}");

    /// <summary>The decorator type names in application order (innermost first) of one pipeline.</summary>
    public static IReadOnlyList<string> GetPipelineSteps(this string pipeline) =>
        pipeline.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("descriptor = ", StringComparison.Ordinal))
            .ToList();

    public static string GetSectionBetween(this string source, string startMarker, string endMarker)
    {
        var startIdx = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIdx < 0)
            throw new InvalidOperationException($"Source does not contain start marker '{startMarker}':{Environment.NewLine}{source}");

        var searchFrom = startIdx + startMarker.Length;
        var endIdx = source.IndexOf(endMarker, searchFrom, StringComparison.Ordinal);
        if (endIdx < 0)
            throw new InvalidOperationException($"Source does not contain end marker '{endMarker}' after '{startMarker}'.");

        return source[startIdx..(endIdx + endMarker.Length)];
    }

    /// <summary>The generated apply method (signature and body) with the given name.</summary>
    public static string GetApplyMethod(this string decoratorRegistry, string methodName)
    {
        var start = decoratorRegistry.IndexOf($" {methodName}<TRequest, TResponse>(", StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException($"No apply method '{methodName}' in:{Environment.NewLine}{decoratorRegistry}");

        var lineStart = decoratorRegistry.LastIndexOf('\n', start) + 1;
        var end = decoratorRegistry.IndexOf("    }", start, StringComparison.Ordinal);
        return decoratorRegistry[lineStart..(end + 5)];
    }

    /// <summary>
    /// The number of handler registrations <c>AddDecoratR()</c> emits for handlers of its own assembly (including
    /// <c>IRequestHandler&lt;TRequest&gt;</c> facades): each one comes with exactly one validation registration.
    /// </summary>
    public static int CountLocalRegistrations(this string registrations) =>
        registrations.CountOccurrences("global::DecoratR.Metadata.DecoratRServiceKeys.Validation, typeof(");

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
}
