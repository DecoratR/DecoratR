namespace DecoratR.Generator.Emit;

/// <summary>
/// Emits the private runtime helpers that wrap service descriptors. The same helpers are emitted into decorator
/// registries (libraries) and into the composition root, so each assembly is self-contained.
/// </summary>
internal static class RuntimeHelpersEmitter
{
    /// <summary>Emits <c>Decorate(services, serviceType, decorate)</c>: replaces all non-keyed registrations of a service type.</summary>
    public static void WriteDecorate(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Replaces every non-keyed registration of <paramref name=\"serviceType\"/> with the descriptor returned by <paramref name=\"decorate\"/>.");
        w.AppendLine("/// </summary>");
        w.AppendLine("private static void Decorate(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceCollection).AppendLine(" services,");
            w.Append(WellKnownTypes.Type).AppendLine(" serviceType,");
            w.Append(WellKnownTypes.Func).Append('<').Append(WellKnownTypes.ServiceDescriptor).Append(", ")
                .Append(WellKnownTypes.ServiceDescriptor).AppendLine("> decorate)");
        }

        using (w.Block())
        {
            w.AppendLine("var decorated = false;");
            using (w.Block("for (var i = 0; i < services.Count; i++)"))
            {
                w.AppendLine("var descriptor = services[i];");
                using (w.Block("if (descriptor.IsKeyedService || descriptor.ServiceType != serviceType)"))
                {
                    w.AppendLine("continue;");
                }

                w.AppendLine();
                w.AppendLine("services[i] = decorate(descriptor);");
                w.AppendLine("decorated = true;");
            }

            w.AppendLine();
            using (w.Block("if (!decorated)"))
            {
                w.Append("throw new ").Append(WellKnownTypes.InvalidOperationException)
                    .AppendLine("($\"DecoratR: no registration of '{serviceType}' was found to decorate.\");");
            }
        }
    }

    /// <summary>Emits <c>Wrap&lt;TService, TDecorator&gt;(inner)</c>: creates a descriptor resolving the decorator around the inner service.</summary>
    public static void WriteWrap(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Returns a descriptor with the lifetime of <paramref name=\"inner\"/> that resolves <typeparamref name=\"TDecorator\"/>");
        w.AppendLine("/// around the service created by <paramref name=\"inner\"/>. Constructor dependencies are resolved from the provider;");
        w.AppendLine("/// constructor selection happens once per registration, not per resolution.");
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
            w.Append("var decoratorFactory = ").Append(WellKnownTypes.ActivatorUtilities).AppendLine(".CreateFactory(");
            using (w.Indent())
            {
                w.AppendLine("typeof(TDecorator),");
                w.Append("new ").Append(WellKnownTypes.Type).AppendLine("[] { typeof(TService) });");
            }

            w.AppendLine();
            w.Append("return ").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(");
            using (w.Indent())
            {
                w.AppendLine("inner.ServiceType,");
                w.AppendLine("provider => decoratorFactory(provider, new object?[] { innerFactory(provider) }),");
                w.AppendLine("inner.Lifetime);");
            }
        }
    }

    /// <summary>Emits <c>CreateInnerFactory(descriptor)</c>: turns any descriptor into a provider-based factory.</summary>
    public static void WriteCreateInnerFactory(SourceWriter w)
    {
        w.AppendLine("/// <summary>");
        w.AppendLine("/// Creates a factory for the service described by <paramref name=\"descriptor\"/>.");
        w.AppendLine("/// </summary>");
        w.Append("private static ").Append(WellKnownTypes.Func).Append('<').Append(WellKnownTypes.ServiceProvider)
            .AppendLine(", object> CreateInnerFactory(");
        using (w.Indent())
        {
            w.Append(WellKnownTypes.ServiceDescriptor).AppendLine(" descriptor)");
        }

        using (w.Block())
        {
            using (w.Block("if (descriptor.ImplementationFactory is not null)"))
            {
                w.AppendLine("return descriptor.ImplementationFactory;");
            }

            w.AppendLine();
            using (w.Block("if (descriptor.ImplementationInstance is not null)"))
            {
                w.AppendLine("var instance = descriptor.ImplementationInstance;");
                w.AppendLine("return _ => instance;");
            }

            w.AppendLine();
            w.Append("var factory = ").Append(WellKnownTypes.ActivatorUtilities).AppendLine(".CreateFactory(");
            using (w.Indent())
            {
                w.AppendLine("descriptor.ImplementationType!,");
                w.Append(WellKnownTypes.Type).AppendLine(".EmptyTypes);");
            }

            w.AppendLine("return provider => factory(provider, null);");
        }
    }
}
