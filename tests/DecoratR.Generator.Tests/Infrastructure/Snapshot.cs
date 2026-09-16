using System.Text.RegularExpressions;

namespace DecoratR.Generator.Tests.Infrastructure;

/// <summary>
/// Minimal file-based snapshot verification. Run the tests with <c>UPDATE_SNAPSHOTS=1</c> to (re)write the
/// <c>*.verified.cs</c> files under <c>Snapshots/</c>.
/// </summary>
public static class Snapshot
{
    private static readonly Regex VersionPattern = new(
        """GeneratedCode\("DecoratR\.Generator", "[^"]*"\)""", RegexOptions.Compiled);

    private static readonly Lazy<string> SnapshotDirectory = new(FindSnapshotDirectory);

    public static void Verify(string name, string actual)
    {
        var directory = SnapshotDirectory.Value;
        Directory.CreateDirectory(directory);

        var verifiedPath = Path.Combine(directory, name + ".verified.cs");
        var receivedPath = Path.Combine(directory, name + ".received.cs");
        var normalized = Normalize(actual);

        var update = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
        if (update || !File.Exists(verifiedPath))
        {
            File.WriteAllText(verifiedPath, normalized);
            if (File.Exists(receivedPath)) File.Delete(receivedPath);
            if (!update)
                throw new InvalidOperationException($"Snapshot '{name}' did not exist and was created at {verifiedPath}. Review it and re-run.");
            return;
        }

        var expected = Normalize(File.ReadAllText(verifiedPath));
        if (expected == normalized)
        {
            if (File.Exists(receivedPath)) File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, normalized);
        throw new InvalidOperationException(
            $"Snapshot '{name}' differs from {verifiedPath}. Received output written to {receivedPath}.{Environment.NewLine}" +
            $"First difference at line {FirstDifferingLine(expected, normalized)}.");
    }

    /// <summary>
    /// Walks up from the test binaries to the project directory. <c>[CallerFilePath]</c> is not usable because
    /// deterministic builds (CI) map source paths to <c>/_/</c>.
    /// </summary>
    private static string FindSnapshotDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DecoratR.Generator.Tests.csproj")))
                return Path.Combine(directory.FullName, "Snapshots");

        throw new InvalidOperationException("Could not locate the DecoratR.Generator.Tests project directory.");
    }

    private static string Normalize(string text) =>
        VersionPattern.Replace(text.Replace("\r\n", "\n").TrimEnd(), """GeneratedCode("DecoratR.Generator", "<version>")""");

    private static int FirstDifferingLine(string expected, string actual)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        for (var i = 0; i < Math.Min(e.Length, a.Length); i++)
            if (e[i] != a[i])
                return i + 1;

        return Math.Min(e.Length, a.Length) + 1;
    }
}
