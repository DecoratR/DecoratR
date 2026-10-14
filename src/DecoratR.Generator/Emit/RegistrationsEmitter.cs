using DecoratR.Generator.Model;

namespace DecoratR.Generator.Emit;

/// <summary>
/// Emits the <c>AddDecoratR()</c> extension method for a composition root.
/// </summary>
internal static class RegistrationsEmitter
{
    public const string HintName = "DecoratRServiceCollectionExtensions.g.cs";

    public static string Generate(
        AssemblyInfo assembly,
        EquatableArray<HandlerMetadata> localHandlers,
        ReferencedRegistrationData referenced,
        DecorationPlan plan,
        int decoratorCount)
    {
        var handlerCount = localHandlers.Length + referenced.Handlers.Length;
        var w = new SourceWriter(4096 + localHandlers.Length * 700 + plan.Pipelines.Count * 300);

        w.AppendFileHeader();
        w.Append("namespace ").Append(WellKnownTypes.ExtensionsNamespace).AppendLine(";");
        w.AppendLine();

        w.AppendLine("/// <summary>");
        w.Append("/// DecoratR registrations generated for assembly <c>").AppendXmlEscaped(assembly.Name).AppendLine("</c>.");
        w.AppendLine("/// </summary>");
        w.AppendGeneratedCodeAttribute();

        using (w.Block("public static class " + WellKnownTypes.ExtensionsClassName))
        {
            WriteAddDecoratR(w, localHandlers, referenced, plan, handlerCount, decoratorCount);

            var usesWrap = false;
            foreach (var local in plan.LocalApplyMethods)
            {
                w.AppendLine();
                FactoryEmitter.WriteApplyMethod(w, local.Decorator, local.Name, isPublic: false);
                usesWrap |= local.Decorator.Constructor is null;
            }

            var hasFactoryRegistries = false;
            foreach (var registry in referenced.Registries) hasFactoryRegistries |= registry.HasFactories;

            if (hasFactoryRegistries)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteAddHandlers(w);
            }

            if (plan.Pipelines.Count > 0)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteDecorate(w);
            }

            if (usesWrap)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteWrap(w);
            }

            if (plan.LocalApplyMethods.Count > 0)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteCreateInnerFactory(w);
            }

            w.AppendLine();
            RuntimeHelpersEmitter.WriteMarker(w);
        }

        return w.ToString();
    }

    private static void WriteAddDecoratR(
        SourceWriter w,
        EquatableArray<HandlerMetadata> localHandlers,
        ReferencedRegistrationData referenced,
        DecorationPlan plan,
        int handlerCount,
        int decoratorCount)
    {
        w.AppendLine("/// <summary>");
        w.Append("/// Registers ").Append(handlerCount).Append(" handler(s) and applies ").Append(decoratorCount)
            .AppendLine(" decorator(s) discovered in this assembly and its references.");
        w.AppendLine("/// Lower <c>Order</c> values are applied further outside; ties are broken by the fully qualified decorator type name.");
        w.AppendLine("/// Calling this method more than once has no further effect.");
        w.AppendLine("/// </summary>");
        w.AppendLine("/// <param name=\"services\">The service collection to add the registrations to.</param>");
        w.Append("/// <param name=\"configure\">Optional callback that configures the <see cref=\"").Append(WellKnownTypes.Options)
            .AppendLine("\"/>, e.g. the handler lifetime.</param>");
        w.AppendLine("/// <returns>The same <paramref name=\"services\"/> instance.</returns>");
        w.Append("public static ").Append(WellKnownTypes.ServiceCollection).AppendLine(" AddDecoratR(");
        using (w.Indent())
        {
            w.Append("this ").Append(WellKnownTypes.ServiceCollection).AppendLine(" services,");
            w.Append(WellKnownTypes.Action).Append('<').Append(WellKnownTypes.Options).AppendLine(">? configure = null)");
        }

        using (w.Block())
        {
            w.AppendLine("// Registering the handlers again would decorate the existing registrations a second time.");
            using (w.Block("for (var i = 0; i < services.Count; i++)"))
            {
                using (w.Block("if (services[i].ServiceType == typeof(Marker))"))
                {
                    w.AppendLine("return services;");
                }
            }

            w.AppendLine();
            w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor).AppendLine("(typeof(Marker), Marker.Instance));");
            w.AppendLine();
            w.Append("var options = new ").Append(WellKnownTypes.Options).AppendLine("();");
            w.AppendLine("configure?.Invoke(options);");
            if (!referenced.Registries.IsEmpty || !localHandlers.IsEmpty) w.AppendLine("var lifetime = options.Lifetime;");

            if (!referenced.Registries.IsEmpty) WriteReferencedHandlers(w, referenced.Registries);

            if (!localHandlers.IsEmpty)
            {
                w.AppendLine();
                w.AppendLine("// Handlers declared in this assembly, each with a validation registration for ValidateOnBuild");
                foreach (var handler in localHandlers)
                {
                    WriteLocalRegistration(w, handler);

                    // Handlers without a response are also exposed as IRequestHandler<TRequest>; the facade
                    // resolves the decorated IRequestHandler<TRequest, Unit> pipeline.
                    if (handler.RegistersVoidFacade) WriteLocalVoidFacadeRegistration(w, handler);
                }
            }

            if (plan.Pipelines.Count > 0)
            {
                w.AppendLine();
                w.AppendLine("// Decorator pipelines (innermost decorator first, lower Order = further outside)");
                w.Append("var pipelines = new ").Append(WellKnownTypes.Dictionary).Append('<').Append(WellKnownTypes.Type)
                    .Append(", Pipeline>(").Append(plan.Pipelines.Count).AppendLine(");");
                foreach (var pipeline in plan.Pipelines) WritePipeline(w, pipeline);
                w.AppendLine("Decorate(services, pipelines);");
            }

            foreach (var skipped in plan.SkippedNonPublicServices)
            {
                w.AppendLine();
                w.Append("// Skipped: decorators for ").Append(skipped.ConstructedInterface)
                    .AppendLine(" cannot be applied because its request or response type is not public (DCTR011).");
            }

            w.AppendLine();
            w.AppendLine("return services;");
        }
    }

    private static void WriteReferencedHandlers(SourceWriter w, EquatableArray<ReferencedRegistry> registries)
    {
        w.AppendLine();
        w.AppendLine("// Handlers from referenced assemblies");
        foreach (var registry in registries)
        {
            if (registry.HasFactories)
            {
                w.Append("AddHandlers(services, ").Append(registry.Type).Append('.').Append(WellKnownTypes.HandlersPropertyName)
                    .AppendLine(", lifetime);");
                continue;
            }

            // Registries of earlier generator versions carry no factories; their handlers are registered by type.
            using (w.Block("foreach (var handler in " + registry.Type + "." + WellKnownTypes.HandlersPropertyName + ")"))
            {
                w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor)
                    .AppendLine("(handler.ServiceType, handler.ImplementationType, lifetime));");
                RuntimeHelpersEmitter.WriteValidationRegistration(w, "handler.ImplementationType");
            }
        }
    }

    private static void WriteLocalRegistration(SourceWriter w, HandlerMetadata handler)
    {
        var serviceType = "typeof(" + handler.ServiceType.ConstructedInterface + ")";
        var implementationType = "typeof(" + handler.HandlerType + ")";

        if (handler.Constructor is null)
        {
            // The constructor cannot be called directly: registered by implementation type.
            w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor).Append('(').Append(serviceType)
                .Append(", ").Append(implementationType).AppendLine(", lifetime));");
        }
        else
        {
            w.Append("services.Add(").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(");
            using (w.Indent())
            {
                w.Append(serviceType).AppendLine(",");
                FactoryEmitter.WriteHandlerFactory(w, handler.ServiceType.ConstructedInterface, handler.HandlerType, handler.Constructor);
                w.AppendLine(",");
                w.AppendLine("lifetime));");
            }
        }

        RuntimeHelpersEmitter.WriteValidationRegistration(w, implementationType);
    }

    private static void WriteLocalVoidFacadeRegistration(SourceWriter w, HandlerMetadata handler)
    {
        w.Append("services.Add(").Append(WellKnownTypes.ServiceDescriptor).AppendLine(".Describe(");
        using (w.Indent())
        {
            w.Append("typeof(").Append(handler.VoidFacadeInterface).AppendLine("),");
            FactoryEmitter.WriteVoidFacadeFactory(w, handler);
            w.AppendLine(",");
            w.AppendLine("lifetime));");
        }

        RuntimeHelpersEmitter.WriteValidationRegistration(w, "typeof(" + handler.VoidFacadeImplementation + ")");
    }

    private static void WritePipeline(SourceWriter w, ServicePipeline pipeline)
    {
        var service = pipeline.Service;

        w.Append("pipelines.Add(typeof(").Append(service.ConstructedInterface).AppendLine("), new(static descriptor =>");
        using (w.Block(closing: "}));"))
        {
            foreach (var step in pipeline.Steps)
            {
                w.Append("descriptor = ").Append(step.ApplyMethod).Append('<').Append(service.RequestType)
                    .Append(", ").Append(service.ResponseType).Append(">(descriptor); // Order ").Append(step.Order).AppendLine();
            }

            w.AppendLine("return descriptor;");
        }
    }
}
