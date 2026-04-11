using AwesomeAssertions;
using DecoratR.Generator.Tests.Fixtures;
using DecoratR.Generator.Tests.Infrastructure;
using Xunit;

namespace DecoratR.Generator.Tests;

public class ConstraintFilteringTests : GeneratorTestBase
{
    [Fact]
    public void ConstrainedDecorator_OnlyAppliedToMatchingRequestType()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.CommandQueryInterfaces}

                      {TestSources.CommandQueryHandlers}

                      {TestSources.Decorator("CommandOnlyDecorator", 1, "ICommand")}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.Should().Contain("CreateUserCommand");
        decorateSection.Should().Contain("CommandOnlyDecorator");
        decorateSection.Should().NotContain("GetUsersQuery");
    }

    [Fact]
    public void UnconstrainedDecorator_AppliedToAllHandlers()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.CommandQueryInterfaces}

                      {TestSources.CommandQueryHandlers}

                      {TestSources.Decorator("LoggingDecorator", 1)}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.Should().Contain("CreateUserCommand");
        decorateSection.Should().Contain("GetUsersQuery");
    }

    [Fact]
    public void MixedConstraints_EachDecoratorAppliedToCorrectHandlers()
    {
        var source = $"""
                      using DecoratR;

                      {TestSources.RegistrationsAttribute}

                      {TestSources.CommandQueryInterfaces}

                      {TestSources.CommandQueryHandlers}

                      {TestSources.Decorator("LoggingDecorator", 1)}

                      {TestSources.Decorator("ValidationDecorator", 2, "ICommand")}
                      """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.CountOccurrences("LoggingDecorator").Should().Be(2);

        decorateSection.CountOccurrences("ValidationDecorator").Should().Be(1);
        decorateSection.Should().Contain("ValidationDecorator<global::CreateUserCommand");
    }

    [Fact]
    public void RequestWithMultipleInterfaces_MatchedByAllApplicableDecorators()
    {
        var source = """
                     using DecoratR;

                     [assembly: DecoratR.GenerateDecoratRRegistrations]

                     public interface ICommand : IRequest;
                     public interface ILoggable;

                     public sealed record LoggableCommand(string Name) : ICommand, ILoggable;

                     public sealed class LoggableCommandHandler : IRequestHandler<LoggableCommand, string>
                     {
                         public ValueTask<string> HandleAsync(LoggableCommand request, CancellationToken cancellationToken = default)
                             => ValueTask.FromResult("handled");
                     }

                     [Decorator(Order = 1)]
                     public class CommandDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : ICommand
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public CommandDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }

                     [Decorator(Order = 2)]
                     public class LoggableDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
                         where TRequest : ILoggable
                     {
                         private readonly IRequestHandler<TRequest, TResponse> _inner;
                         public LoggableDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;
                         public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                             => _inner.HandleAsync(request, cancellationToken);
                     }
                     """;

        var (_, generatedTrees) = RunGenerator(source);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.Should().Contain("CommandDecorator");
        decorateSection.Should().Contain("LoggableDecorator");
    }

    [Fact]
    public void CrossAssembly_ConstrainedDecoratorAppliedOnlyToMatchingHandlers()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            $$"""
              using DecoratR;

              {{TestSources.MetadataAttribute}}

              {{TestSources.CommandQueryInterfaces}}

              public sealed record RemoteCommand(string Name) : ICommand;
              public sealed record RemoteQuery : IQuery;

              public sealed class RemoteCommandHandler : IRequestHandler<RemoteCommand, string>
              {
                  public ValueTask<string> HandleAsync(RemoteCommand request, CancellationToken cancellationToken = default)
                      => ValueTask.FromResult("handled");
              }

              public sealed class RemoteQueryHandler : IRequestHandler<RemoteQuery, string>
              {
                  public ValueTask<string> HandleAsync(RemoteQuery request, CancellationToken cancellationToken = default)
                      => ValueTask.FromResult("queried");
              }

              {{TestSources.Decorator("CommandDecorator", 1, "ICommand")}}
              """,
            $$"""
              using DecoratR;

              {{TestSources.RegistrationsAttribute}}
              """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.Should().Contain("RemoteCommand");
        decorateSection.Should().NotContain("RemoteQuery");
    }

    [Fact]
    public void CrossAssembly_ApplyMethodHasSpecificConstraint()
    {
        var source = $$"""
                       using DecoratR;

                       {{TestSources.MetadataAttribute}}

                       public interface ICommand : IRequest;

                       public sealed record TestCommand(string Name) : ICommand;

                       public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
                       {
                           public ValueTask<string> HandleAsync(TestCommand request, CancellationToken cancellationToken = default)
                               => ValueTask.FromResult("Hello");
                       }

                       {{TestSources.Decorator("CommandDecorator", 1, "ICommand")}}
                       """;

        var (_, generatedTrees) = RunGenerator(source, "HandlerLib");

        var decoratorRegistry = generatedTrees.FindSource("DecoratRDecoratorRegistry");

        var applyMethod = decoratorRegistry.GetSectionBetween(
            "public static void ApplyCommandDecorator", "}");
        applyMethod.Should().Contain("where TRequest : global::ICommand");
        applyMethod.Should().NotContain("where TRequest : global::DecoratR.IRequest");

        decoratorRegistry.Should().Contain("RequestConstraintTypes");
        decoratorRegistry.Should().Contain("global::ICommand");
    }

    [Fact]
    public void CrossAssembly_ConstrainedDecoratorCorrectlyFiltersLocalHandlers()
    {
        var (_, generatedTrees) = RunTwoStageGenerator(
            $$"""
              using DecoratR;

              {{TestSources.MetadataAttribute}}

              public interface ICommand : IRequest;

              public sealed record RemoteCommand(string Name) : ICommand;

              public sealed class RemoteCommandHandler : IRequestHandler<RemoteCommand, string>
              {
                  public ValueTask<string> HandleAsync(RemoteCommand request, CancellationToken cancellationToken = default)
                      => ValueTask.FromResult("remote");
              }

              {{TestSources.Decorator("CommandDecorator", 1, "ICommand")}}
              """,
            """
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record LocalQuery : IRequest;

            public sealed class LocalQueryHandler : IRequestHandler<LocalQuery, string>
            {
                public ValueTask<string> HandleAsync(LocalQuery request, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult("local");
            }
            """);

        var registrations = generatedTrees.FindSource("DecoratRServiceCollectionExtensions");
        var decorateSection = registrations.GetDecoratorApplicationSection();

        decorateSection.Should().Contain("RemoteCommand");
        decorateSection.Should().NotContain("LocalQuery");
    }
}