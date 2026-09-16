using System.ComponentModel;

namespace DecoratR.Metadata;

/// <summary>
/// Infrastructure attribute emitted by the DecoratR source generator. Describes one handler of an assembly so
/// that a composition root can apply decorators to its service type across assembly boundaries.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DecoratRHandlerAttribute(string handlerType, string requestType, string responseType, bool isStream)
    : Attribute
{
    /// <summary>The fully qualified name of the handler implementation type.</summary>
    public string HandlerType { get; } = handlerType;

    /// <summary>The fully qualified name of the request type.</summary>
    public string RequestType { get; } = requestType;

    /// <summary>The fully qualified name of the response type.</summary>
    public string ResponseType { get; } = responseType;

    /// <summary>
    /// <see langword="true"/> for <see cref="IStreamRequestHandler{TRequest, TResponse}"/> implementations,
    /// <see langword="false"/> for <see cref="IRequestHandler{TRequest, TResponse}"/> implementations.
    /// </summary>
    public bool IsStream { get; } = isStream;

    /// <summary>
    /// Semicolon-delimited fully qualified names of the request type, its interfaces and base types plus type facts
    /// (<c>!class</c>, <c>!struct</c>, <c>!unmanaged</c>, <c>!new</c>). Used for decorator constraint matching.
    /// </summary>
    public string RequestTypeHierarchy { get; init; } = "";

    /// <summary>
    /// Semicolon-delimited fully qualified names of the response type, its interfaces and base types plus type
    /// facts. Used for decorator constraint matching.
    /// </summary>
    public string ResponseTypeHierarchy { get; init; } = "";

    /// <summary>
    /// <see langword="true"/> when both request and response type are publicly accessible and the service type can
    /// therefore be decorated from another assembly.
    /// </summary>
    public bool IsPubliclyAccessible { get; init; } = true;
}
