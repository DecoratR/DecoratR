using DecoratR.Generator.Detection;
using DecoratR.Generator.Model;

namespace DecoratR.Generator.Emit;

/// <summary>
/// Emits the direct constructor calls of generated handler factories and apply methods (see
/// <see cref="ConstructorAnalyzer"/>), and the apply methods themselves.
/// </summary>
internal static class FactoryEmitter
{
    /// <summary>The service provider parameter of every generated factory lambda.</summary>
    public const string Provider = "sp";

    /// <summary>
    /// Writes <c>(Func&lt;IServiceProvider, TService&gt;)(static sp =&gt; new Handler(…))</c>, the factory of a handler that
    /// can be constructed directly. The delegate is typed with the service type, so the decorator wrapping it calls it
    /// without a cast (see <see cref="WriteApplyMethod"/>).
    /// </summary>
    public static void WriteHandlerFactory(SourceWriter w, string serviceType, string handlerType, ConstructorPlan constructor)
    {
        w.Append("(").Append(WellKnownTypes.Func).Append('<').Append(WellKnownTypes.ServiceProvider).Append(", ").Append(serviceType)
            .Append(">)(static ").Append(Provider).Append(" => ");
        WriteNew(w, handlerType, constructor, isDecorator: false);
        w.Append(')');
    }

    /// <summary>
    /// Writes the factory of the <c>VoidRequestHandler&lt;TRequest&gt;</c> facade, which resolves the decorated
    /// <c>IRequestHandler&lt;TRequest, Unit&gt;</c> pipeline of a handler without a response.
    /// </summary>
    public static void WriteVoidFacadeFactory(SourceWriter w, HandlerMetadata handler)
    {
        w.Append("static ").Append(Provider).Append(" => new ").Append(handler.VoidFacadeImplementation).Append('(')
            .Append(WellKnownTypes.GetRequiredService).Append('<').Append(handler.ServiceType.ConstructedInterface)
            .Append(">(").Append(Provider).Append("))");
    }

    /// <summary>
    /// Writes a generic apply method that wraps a <c>ServiceDescriptor</c> of the decorator's handler interface with
    /// the decorator. Decorators that can be constructed directly get a factory lambda; the others go through
    /// <c>Wrap</c> (<c>ActivatorUtilities</c>, compiled on first resolution).
    /// </summary>
    public static void WriteApplyMethod(SourceWriter w, DecoratorMetadata decorator, string methodName, bool isPublic)
    {
        var handlerInterface = WellKnownTypes.HandlerInterface(decorator.IsStream) + "<TRequest, TResponse>";
        var decoratorType = decorator.Construct("TRequest", "TResponse");

        w.AppendLine("/// <summary>");
        w.Append("/// Wraps a <c>").AppendTypeForDoc(handlerInterface).Append("</c> registration with <c>")
            .AppendTypeForDoc(decoratorType).Append("</c> (Order ").Append(decorator.Order).AppendLine(").");
        w.AppendLine("/// </summary>");
        w.Append(isPublic ? "public" : "private").Append(" static ").Append(WellKnownTypes.ServiceDescriptor).Append(' ')
            .Append(methodName).AppendLine("<TRequest, TResponse>(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceDescriptor).AppendLine(" inner)");
            ConstraintSerializer.WriteWhereClause(w, "TRequest", decorator.RequestConstraints, "TRequest", "TResponse");
            ConstraintSerializer.WriteWhereClause(w, "TResponse", decorator.ResponseConstraints, "TRequest", "TResponse");
        }

        using (w.Block())
        {
            if (decorator.Constructor is null)
            {
                w.Append("return Wrap<").Append(handlerInterface).Append(", ").Append(decoratorType).AppendLine(">(inner);");
                return;
            }

            var typedFactory = WellKnownTypes.Func + "<" + WellKnownTypes.ServiceProvider + ", " + handlerInterface + ">";

            // Generated handler factories and apply methods return delegates typed with the handler interface, so the
            // inner one is called without an interface cast: measured about 5 ns less per resolution of a three-layer
            // pipeline under JIT and NativeAOT (docs/modular-monolith-analysis.md, section 4.1). Any other registration
            // is cast per resolution.
            w.AppendLine("var factory = CreateInnerFactory(inner);");
            w.Append("var innerFactory = factory as ").Append(typedFactory).Append(" ?? (").Append(Provider).Append(" => (")
                .Append(handlerInterface).Append(")factory(").Append(Provider).AppendLine("));");
            w.Append("return ").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(");
            using (w.Indent())
            {
                w.AppendLine("inner.ServiceType,");
                w.Append('(').Append(typedFactory).Append(")(").Append(Provider).Append(" => ");
                WriteNew(w, decoratorType, decorator.Constructor, isDecorator: true);
                w.AppendLine("),");
                w.AppendLine("inner.Lifetime);");
            }
        }
    }

    /// <summary>
    /// Writes <c>new T(arg1, …)</c> without a trailing separator. Arguments go on their own lines; for decorators,
    /// the <c>{TRequest}</c>/<c>{TResponse}</c> placeholders become the apply method's type parameters.
    /// </summary>
    private static void WriteNew(SourceWriter w, string type, ConstructorPlan constructor, bool isDecorator)
    {
        w.Append("new ").Append(type).Append('(');

        var arguments = constructor.Arguments;
        if (arguments.IsEmpty)
        {
            w.Append(')');
            return;
        }

        w.AppendLine();
        using (w.Indent())
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0) w.AppendLine(",");
                WriteArgument(w, arguments[i], isDecorator);
            }

            w.Append(')');
        }
    }

    private static void WriteArgument(SourceWriter w, ConstructorArgument argument, bool isDecorator)
    {
        var type = isDecorator ? ConstraintSerializer.Substitute(argument.Type, "TRequest", "TResponse") : argument.Type;

        switch (argument.Kind)
        {
            case ConstructorArgumentKind.InnerHandler:
                w.Append("innerFactory(").Append(Provider).Append(')');
                break;

            case ConstructorArgumentKind.ServiceProvider:
                w.Append(Provider);
                break;

            case ConstructorArgumentKind.KeyedService:
                w.Append(WellKnownTypes.GetRequiredKeyedService).Append('<').Append(type).Append(">(")
                    .Append(Provider).Append(", ").Append(argument.Key!).Append(')');
                break;

            default:
                w.Append(WellKnownTypes.GetRequiredService).Append('<').Append(type).Append(">(").Append(Provider).Append(')');
                break;
        }
    }
}
