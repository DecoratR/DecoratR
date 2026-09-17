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
        var w = new SourceWriter(4096 + handlerCount * 300 + plan.Pipelines.Count * 300);

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

            if (plan.Pipelines.Count > 0)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteDecorate(w);
            }

            if (plan.UsesLocalDecorators)
            {
                w.AppendLine();
                RuntimeHelpersEmitter.WriteWrap(w);
                w.AppendLine();
                RuntimeHelpersEmitter.WriteCreateInnerFactory(w);
            }
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
            w.Append("var options = new ").Append(WellKnownTypes.Options).AppendLine("();");
            w.AppendLine("configure?.Invoke(options);");

            if (!referenced.RegistryTypes.IsEmpty)
            {
                w.AppendLine();
                w.AppendLine("// Handlers from referenced assemblies");
                foreach (var registry in referenced.RegistryTypes)
                {
                    using (w.Block("foreach (var handler in " + registry + ".Handlers)"))
                    {
                        w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor)
                            .AppendLine("(handler.ServiceType, handler.ImplementationType, options.Lifetime));");
                    }
                }
            }

            if (!localHandlers.IsEmpty)
            {
                w.AppendLine();
                w.AppendLine("// Handlers declared in this assembly");
                foreach (var handler in localHandlers)
                {
                    WriteRegistration(w, handler.ServiceType.ConstructedInterface, handler.HandlerType);

                    // Handlers without a response are also exposed as IRequestHandler<TRequest>; the facade
                    // resolves the decorated IRequestHandler<TRequest, Unit> pipeline.
                    if (handler.RegistersVoidFacade)
                        WriteRegistration(w, handler.VoidFacadeInterface, handler.VoidFacadeImplementation);
                }
            }

            if (plan.Pipelines.Count > 0)
            {
                w.AppendLine();
                w.AppendLine("// Decorator pipelines (innermost decorator first, lower Order = further outside)");
                foreach (var pipeline in plan.Pipelines) WritePipeline(w, pipeline);
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

    private static void WriteRegistration(SourceWriter w, string serviceType, string implementationType)
    {
        w.Append("services.Add(new ").Append(WellKnownTypes.ServiceDescriptor).AppendLine("(");
        using (w.Indent())
        {
            w.Append("typeof(").Append(serviceType).AppendLine("),");
            w.Append("typeof(").Append(implementationType).AppendLine("),");
            w.AppendLine("options.Lifetime));");
        }
    }

    private static void WritePipeline(SourceWriter w, ServicePipeline pipeline)
    {
        var service = pipeline.Service.ConstructedInterface;

        w.Append("Decorate(services, typeof(").Append(service).AppendLine("), static descriptor =>");
        using (w.Block(closing: "});"))
        {
            foreach (var step in pipeline.Steps)
            {
                if (step.LocalConstructedType is not null)
                    w.Append("descriptor = Wrap<").Append(service).Append(", ").Append(step.LocalConstructedType).Append(">(descriptor);");
                else
                    w.Append("descriptor = ").Append(step.ApplyMethod!).Append('<').Append(pipeline.Service.RequestType)
                        .Append(", ").Append(pipeline.Service.ResponseType).Append(">(descriptor);");

                w.Append(" // Order ").Append(step.Order).AppendLine();
            }

            w.AppendLine("return descriptor;");
        }
    }
}
