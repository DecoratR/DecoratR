namespace DecoratR.Generator.Emit;

/// <summary>
/// Emits the private runtime helpers of generated registries and composition roots. Every assembly gets its own
/// copy, so each one is self-contained.
/// </summary>
internal static class RuntimeHelpersEmitter
{
    private const string DescriptorFunc =
        WellKnownTypes.Func + "<" + WellKnownTypes.ServiceDescriptor + ", " + WellKnownTypes.ServiceDescriptor + ">";

    /// <summary>
    /// Emits <c>Decorate(services, pipelines)</c> and its <c>Pipeline</c> entry type: one pass over the service
    /// collection that replaces every non-keyed registration of a decorated service type.
    /// </summary>
    public static void WriteDecorate(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Replaces every non-keyed registration of a service type in <paramref name=\"pipelines\"/> with the descriptor");
        w.AppendLine("/// returned by its pipeline, in a single pass over <paramref name=\"services\"/>.");
        w.AppendLine("/// </summary>");
        w.AppendLine("private static void Decorate(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceCollection).AppendLine(" services,");
            w.Append(WellKnownTypes.Dictionary).Append('<').Append(WellKnownTypes.Type).AppendLine(", Pipeline> pipelines)");
        }

        using (w.Block())
        {
            w.AppendLine("var applied = 0;");
            using (w.Block("for (var i = 0; i < services.Count; i++)"))
            {
                w.AppendLine("var descriptor = services[i];");
                using (w.Block("if (descriptor.IsKeyedService)"))
                {
                    w.AppendLine("continue;");
                }

                w.AppendLine();
                w.Append("ref var pipeline = ref ").Append(WellKnownTypes.CollectionsMarshal)
                    .AppendLine(".GetValueRefOrNullRef(pipelines, descriptor.ServiceType);");
                using (w.Block("if (" + WellKnownTypes.Unsafe + ".IsNullRef(ref pipeline))"))
                {
                    w.AppendLine("continue;");
                }

                w.AppendLine();
                w.AppendLine("services[i] = pipeline.Apply(descriptor);");
                using (w.Block("if (!pipeline.Applied)"))
                {
                    w.AppendLine("pipeline.Applied = true;");
                    w.AppendLine("applied++;");
                }
            }

            w.AppendLine();
            using (w.Block("if (applied == pipelines.Count)"))
            {
                w.AppendLine("return;");
            }

            w.AppendLine();
            w.Append("var missing = new ").Append(WellKnownTypes.List).Append('<').Append(WellKnownTypes.Type).AppendLine(">();");
            using (w.Block("foreach (var pair in pipelines)"))
            {
                using (w.Block("if (!pair.Value.Applied)"))
                {
                    w.AppendLine("missing.Add(pair.Key);");
                }
            }

            w.AppendLine();
            w.Append("throw new ").Append(WellKnownTypes.InvalidOperationException)
                .AppendLine("($\"DecoratR: no registration of '{string.Join(\"', '\", missing)}' was found to decorate.\");");
        }

        w.AppendLine();
        w.AppendLine("/// <summary>The decorators of one service type (innermost first) and whether they were applied.</summary>");
        using (w.Block("private struct Pipeline"))
        {
            w.Append("public readonly ").Append(DescriptorFunc).AppendLine(" Apply;");
            w.AppendLine("public bool Applied;");
            w.AppendLine();
            w.Append("public Pipeline(").Append(DescriptorFunc).AppendLine(" apply)");
            using (w.Block())
            {
                w.AppendLine("Apply = apply;");
                w.AppendLine("Applied = false;");
            }
        }
    }

    /// <summary>
    /// Emits <c>AddHandlers(services, handlers, lifetime)</c>: registers the entries of a referenced handler registry,
    /// each with a validation registration.
    /// </summary>
    public static void WriteAddHandlers(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Registers the handlers of a referenced handler registry: through the generated factory where there is one,");
        w.AppendLine("/// by implementation type otherwise, plus one validation registration per handler.");
        w.AppendLine("/// </summary>");
        w.AppendLine("private static void AddHandlers(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceCollection).AppendLine(" services,");
            w.Append(WellKnownTypes.ReadOnlySpan).Append('<').Append(WellKnownTypes.HandlerRegistration).AppendLine("> handlers,");
            w.Append(WellKnownTypes.ServiceLifetime).AppendLine(" lifetime)");
        }

        using (w.Block())
        {
            using (w.Block("foreach (ref readonly var handler in handlers)"))
            {
                w.AppendLine("services.Add(handler.Factory is { } factory");
                using (w.Indent())
                {
                    w.Append("? ").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(handler.ServiceType, factory, lifetime)");
                    w.Append(": new ").Append(WellKnownTypes.ServiceDescriptor).AppendLine("(handler.ServiceType, handler.ImplementationType, lifetime));");
                }

                WriteValidationRegistration(w, "handler.ImplementationType");
            }
        }
    }

    /// <summary>
    /// Emits the never-resolved keyed registration through which <c>ValidateOnBuild</c>/<c>ValidateScopes</c> check the
    /// dependencies of a handler that is resolved through a (decorated) factory.
    /// </summary>
    public static void WriteValidationRegistration(SourceWriter w, string implementationTypeExpression)
    {
        w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor).Append('(').Append(implementationTypeExpression)
            .Append(", ").Append(WellKnownTypes.ValidationKey).Append(", ").Append(implementationTypeExpression).AppendLine(", lifetime));");
    }

    /// <summary>
    /// Emits the <c>Marker</c> type whose registration makes repeated <c>AddDecoratR()</c> calls no-ops.
    /// </summary>
    public static void WriteMarker(SourceWriter w)
    {
        w.AppendLine("/// <summary>Registered by the first <c>AddDecoratR()</c> call; further calls return early.</summary>");
        using (w.Block("private sealed class Marker"))
        {
            w.AppendLine("public static readonly Marker Instance = new();");
        }
    }

    /// <summary>
    /// Emits <c>Wrap&lt;TService, TDecorator&gt;(inner)</c>: the <c>ActivatorUtilities</c> fallback for decorators whose
    /// constructor the generator cannot call directly.
    /// </summary>
    public static void WriteWrap(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Returns a descriptor with the lifetime of <paramref name=\"inner\"/> that resolves <typeparamref name=\"TDecorator\"/>");
        w.AppendLine("/// around the service created by <paramref name=\"inner\"/> through <c>ActivatorUtilities</c>. The constructor is selected");
        w.AppendLine("/// once, on the first resolution, instead of at startup.");
        w.AppendLine("/// </summary>");
        w.Append("private static ").Append(WellKnownTypes.ServiceDescriptor).Append(" Wrap<TService, [")
            .Append(WellKnownTypes.DynamicallyAccessedPublicConstructors).AppendLine("] TDecorator>(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceDescriptor).AppendLine(" inner)");
            w.AppendLine("where TService : class");
            w.AppendLine("where TDecorator : class, TService");
        }

        using (w.Block())
        {
            w.AppendLine("var innerFactory = CreateInnerFactory(inner);");
            w.Append(WellKnownTypes.ObjectFactory).AppendLine("? decoratorFactory = null;");
            w.AppendLine();
            w.Append("return ").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(");
            using (w.Indent())
            {
                w.AppendLine("inner.ServiceType,");
                w.Append(FactoryEmitter.Provider).Append(" => (decoratorFactory ??= ").Append(WellKnownTypes.ActivatorUtilities)
                    .Append(".CreateFactory(typeof(TDecorator), new ").Append(WellKnownTypes.Type).AppendLine("[] { typeof(TService) }))(");
                using (w.Indent())
                {
                    w.Append(FactoryEmitter.Provider).AppendLine(",");
                    w.Append("new object?[] { innerFactory(").Append(FactoryEmitter.Provider).AppendLine(") }),");
                }

                w.AppendLine("inner.Lifetime);");
            }
        }
    }

    /// <summary>Emits <c>CreateInnerFactory(descriptor)</c>: turns any non-keyed descriptor into a provider-based factory.</summary>
    public static void WriteCreateInnerFactory(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Creates a factory for the service described by <paramref name=\"descriptor\"/>. Registrations by implementation type");
        w.AppendLine("/// go through <c>ActivatorUtilities</c>; their constructor is selected on the first resolution instead of at startup.");
        w.AppendLine("/// </summary>");
        w.Append("private static ").Append(WellKnownTypes.Func).Append('<').Append(WellKnownTypes.ServiceProvider)
            .AppendLine(", object> CreateInnerFactory(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceDescriptor).AppendLine(" descriptor)");
        }

        using (w.Block())
        {
            using (w.Block("if (descriptor.ImplementationFactory is { } implementationFactory)"))
            {
                w.AppendLine("return implementationFactory;");
            }

            w.AppendLine();
            using (w.Block("if (descriptor.ImplementationInstance is { } instance)"))
            {
                w.AppendLine("return _ => instance;");
            }

            w.AppendLine();
            w.Append(WellKnownTypes.ObjectFactory).AppendLine("? factory = null;");
            w.Append("return ").Append(FactoryEmitter.Provider).Append(" => (factory ??= ").Append(WellKnownTypes.ActivatorUtilities)
                .Append(".CreateFactory(descriptor.ImplementationType!, ").Append(WellKnownTypes.Type).Append(".EmptyTypes))(")
                .Append(FactoryEmitter.Provider).AppendLine(", null);");
        }
    }

}
