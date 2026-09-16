namespace DecoratR.Generator.Model;

/// <summary>
/// Whether one of the assembly-level trigger attributes is present, and where it is applied.
/// </summary>
internal sealed record GenerationTrigger(bool IsPresent, LocationInfo? Location)
{
    public static readonly GenerationTrigger Absent = new(false, null);
}
