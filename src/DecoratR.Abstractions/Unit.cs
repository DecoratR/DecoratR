namespace DecoratR;

/// <summary>
/// The response type of handlers that produce no result, see <see cref="IRequestHandler{TRequest}" />.
/// Handler authors and callers never deal with it directly; decorators see it as their <c>TResponse</c> type
/// argument when they wrap a handler without a response.
/// </summary>
public readonly record struct Unit
{
    /// <summary>The single <see cref="Unit" /> value.</summary>
    public static readonly Unit Value;

    /// <inheritdoc />
    public override string ToString() => "()";
}
