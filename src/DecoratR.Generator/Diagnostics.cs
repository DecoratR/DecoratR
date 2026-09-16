using Microsoft.CodeAnalysis;

namespace DecoratR.Generator;

internal static class Diagnostics
{
    private const string Category = "DecoratR.Generator";
    private const string HelpLinkBase = "https://github.com/DecoratR/DecoratR/blob/main/docs/guide.md#";

    public static readonly DiagnosticDescriptor NothingFound = Create(
        "DCTR001",
        "No handlers or decorators found",
        "Assembly '{0}' is marked with [{1}] but no handlers or decorators were found",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor HandlersDiscovered = Create(
        "DCTR002",
        "Handlers discovered",
        "DecoratR discovered {0} handler(s) for assembly '{1}'",
        DiagnosticSeverity.Hidden);

    public static readonly DiagnosticDescriptor DecoratorsDiscovered = Create(
        "DCTR003",
        "Decorators discovered",
        "DecoratR discovered {0} decorator(s) for assembly '{1}'",
        DiagnosticSeverity.Hidden);

    public static readonly DiagnosticDescriptor DecoratorMissingHandlerInterface = Create(
        "DCTR004",
        "Decorator does not implement exactly one handler interface",
        "Decorator '{0}' must implement exactly one of IRequestHandler<TRequest, TResponse> or IStreamRequestHandler<TRequest, TResponse>, but it implements {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DecoratorTypeParameterMismatch = Create(
        "DCTR005",
        "Decorator type parameters do not map to the handler interface",
        "Decorator '{0}' must declare exactly two type parameters that are used as the request and response type arguments of its handler interface",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DecoratorIgnored = Create(
        "DCTR006",
        "Decorator is ignored",
        "Decorator '{0}' is ignored because it {1}",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor DuplicateHandler = Create(
        "DCTR007",
        "Multiple handlers for the same service type",
        "Multiple handlers implement '{0}': {1}. Only one handler per request/response pair is supported because only the last registration is resolved.",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor TypeNotAccessible = Create(
        "DCTR008",
        "Type is not accessible from generated code",
        "'{0}' is skipped because it is not accessible from generated code; handlers and decorators must be public or internal and must not be nested in a less accessible type",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor MissingDependencyInjection = Create(
        "DCTR009",
        "Microsoft.Extensions.DependencyInjection.Abstractions is not referenced",
        "Assembly '{0}' is marked with [{1}] but does not reference Microsoft.Extensions.DependencyInjection.Abstractions; DecoratR cannot generate registration code",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor HandlerIsValueType = Create(
        "DCTR010",
        "Handler is a value type",
        "Handler '{0}' is skipped because it is a value type; handlers must be classes",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor ServiceTypeNotPublic = Create(
        "DCTR011",
        "Decorators cannot be applied to a referenced service type with non-public types",
        "Decorators are not applied to '{0}' because its request or response type is not public in the declaring assembly",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor DecoratorMissingInnerConstructor = Create(
        "DCTR012",
        "Decorator has no constructor accepting the inner handler",
        "Decorator '{0}' has no public constructor with a parameter of type '{1}'; the generated pipeline will fail when the decorator is resolved",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor HandlerServiceTypeNotPublic = Create(
        "DCTR013",
        "Request or response type of a handler is not public",
        "The request or response type of handler '{0}' is not public; decorators from other assemblies will not be applied to '{1}'",
        DiagnosticSeverity.Info);

    public static readonly DiagnosticDescriptor DecoratorConstraintNotPublic = Create(
        "DCTR014",
        "Decorator constraint type is not public",
        "Decorator '{0}' is not exported for other assemblies because its constraint type '{1}' is not public; make the constraint type public or apply the decorator only from this assembly",
        DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, DiagnosticSeverity severity) =>
        new(id, title, messageFormat, Category, severity, isEnabledByDefault: true,
            helpLinkUri: HelpLinkBase + id.ToLowerInvariant());
}
