namespace DecoratR.Generator.Model;

/// <summary>
/// A decorator read from the assembly-level metadata of a referenced assembly.
/// </summary>
internal sealed record ReferencedDecoratorInfo(
    string ApplyMethod,
    string DecoratorType,
    int Order,
    bool IsStream,
    EquatableArray<string> RequestConstraints,
    EquatableArray<string> ResponseConstraints)
{
    public static int Compare(ReferencedDecoratorInfo a, ReferencedDecoratorInfo b)
    {
        var cmp = a.Order.CompareTo(b.Order);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.DecoratorType, b.DecoratorType);
    }
}
