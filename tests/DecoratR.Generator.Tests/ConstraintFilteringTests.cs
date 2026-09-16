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
        var registrations = RunGenerator($"""
            using DecoratR;

            {TestSources.RegistrationsAttribute}

            {TestSources.CommandQueryInterfaces}

            {TestSources.CommandQueryHandlers}

            {TestSources.Decorator("CommandOnlyDecorator", 1, "ICommand")}
            """).ShouldCompile().Registrations;

        registrations.GetPipeline("global::CreateUserCommand", "string").Should().Contain("CommandOnlyDecorator");
        registrations.TryGetPipeline("global::GetUsersQuery", "string").Should().BeNull();
    }

    [Fact]
    public void UnconstrainedDecorator_AppliedToAllHandlers()
    {
        var registrations = RunGenerator($"""
            using DecoratR;

            {TestSources.RegistrationsAttribute}

            {TestSources.CommandQueryInterfaces}

            {TestSources.CommandQueryHandlers}

            {TestSources.Decorator("LoggingDecorator", 1)}
            """).ShouldCompile().Registrations;

        registrations.GetPipeline("global::CreateUserCommand", "string").Should().Contain("LoggingDecorator");
        registrations.GetPipeline("global::GetUsersQuery", "string").Should().Contain("LoggingDecorator");
    }

    [Fact]
    public void MixedConstraints_EachDecoratorAppliedToCorrectHandlers()
    {
        var registrations = RunGenerator($"""
            using DecoratR;

            {TestSources.RegistrationsAttribute}

            {TestSources.CommandQueryInterfaces}

            {TestSources.CommandQueryHandlers}

            {TestSources.Decorator("LoggingDecorator", 1)}

            {TestSources.Decorator("ValidationDecorator", 2, "ICommand")}
            """).ShouldCompile().Registrations;

        var section = registrations.GetDecoratorSection();
        section.CountOccurrences("global::LoggingDecorator<").Should().Be(2);
        section.CountOccurrences("global::ValidationDecorator<").Should().Be(1);
        section.Should().Contain("global::ValidationDecorator<global::CreateUserCommand, string>");
    }

    [Fact]
    public void RequestWithMultipleInterfaces_MatchedByAllApplicableDecorators()
    {
        var registrations = RunGenerator("""
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
            public class CommandDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : ICommand
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }

            [Decorator(Order = 2)]
            public class LoggableDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, ILoggable
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """).ShouldCompile().Registrations;

        var steps = registrations.GetPipeline("global::LoggableCommand", "string").GetPipelineSteps();
        steps.Should().HaveCount(2);
        steps[0].Should().Contain("LoggableDecorator");
        steps[1].Should().Contain("CommandDecorator");
    }

    [Fact]
    public void MultipleTypeConstraints_AreAllEmittedInApplyMethod()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public interface ICommand : IRequest;
            public interface IAuditable;

            public sealed record AuditedCommand : ICommand, IAuditable;

            public sealed class AuditedHandler : IRequestHandler<AuditedCommand, string>
            {
                public ValueTask<string> HandleAsync(AuditedCommand request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public class AuditDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : ICommand, IAuditable
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """, "HandlerLib").ShouldCompile();

        var applyMethod = result.DecoratorRegistry.GetApplyMethod("ApplyAuditDecorator");
        applyMethod.Should().Contain("where TRequest : global::ICommand, global::IAuditable");
        result.DecoratorRegistry.Should().Contain("RequestConstraints = \"global::ICommand;global::IAuditable\"");
    }

    [Fact]
    public void MarkerPlusInterfaceConstraint_KeepsMarkerInApplyMethod()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public interface IAuditable;

            public sealed record AuditedCommand : IRequest, IAuditable;

            public sealed class AuditedHandler : IRequestHandler<AuditedCommand, string>
            {
                public ValueTask<string> HandleAsync(AuditedCommand request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public class AuditDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest, IAuditable
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """, "HandlerLib").ShouldCompile();

        result.DecoratorRegistry.GetApplyMethod("ApplyAuditDecorator")
            .Should().Contain("where TRequest : global::DecoratR.IRequest, global::IAuditable");
    }

    [Fact]
    public void SpecialConstraints_AreEmittedAndMatched()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public sealed record Parameterless : IRequest;
            public sealed record WithParameter(string Name) : IRequest;
            public readonly record struct StructRequest : IRequest;

            public sealed class ParameterlessHandler : IRequestHandler<Parameterless, int>
            {
                public ValueTask<int> HandleAsync(Parameterless request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class WithParameterHandler : IRequestHandler<WithParameter, string>
            {
                public ValueTask<string> HandleAsync(WithParameter request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class StructRequestHandler : IRequestHandler<StructRequest, string>
            {
                public ValueTask<string> HandleAsync(StructRequest request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public class NewDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : class, IRequest, new()
                where TResponse : struct
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }

            [Decorator(Order = 2)]
            public class StructDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : struct, IRequest
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """).ShouldCompile();

        var registrations = result.Registrations;
        registrations.GetPipeline("global::Parameterless", "int").Should().Contain("NewDecorator");
        registrations.TryGetPipeline("global::WithParameter", "string").Should().BeNull("WithParameter has no parameterless constructor and a class response");
        registrations.GetPipeline("global::StructRequest", "string").GetPipelineSteps().Should().ContainSingle().Which.Should().Contain("StructDecorator");
    }

    [Fact]
    public void ResponseConstraint_FiltersByResponseType()
    {
        var registrations = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public interface IResult;
            public sealed class UserResult : IResult;

            public sealed record GetUser : IRequest;
            public sealed record GetName : IRequest;

            public sealed class GetUserHandler : IRequestHandler<GetUser, UserResult>
            {
                public ValueTask<UserResult> HandleAsync(GetUser request, CancellationToken cancellationToken = default) => default;
            }

            public sealed class GetNameHandler : IRequestHandler<GetName, string>
            {
                public ValueTask<string> HandleAsync(GetName request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public class ResultDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest
                where TResponse : IResult
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """).ShouldCompile().Registrations;

        registrations.GetPipeline("global::GetUser", "global::UserResult").Should().Contain("ResultDecorator");
        registrations.TryGetPipeline("global::GetName", "string").Should().BeNull();
    }

    [Fact]
    public void GenericInterfaceConstraint_IsMatchedWithSubstitutedTypeParameters()
    {
        var registrations = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRRegistrations]

            public interface IQuery<TResult> : IRequest;

            public sealed record GetName : IQuery<string>;
            public sealed record GetCount : IQuery<int>;

            public sealed class GetNameHandler : IRequestHandler<GetName, string>
            {
                public ValueTask<string> HandleAsync(GetName request, CancellationToken cancellationToken = default) => default;
            }

            // Response type does not match the IQuery<TResult> argument on purpose.
            public sealed class GetCountHandler : IRequestHandler<GetCount, string>
            {
                public ValueTask<string> HandleAsync(GetCount request, CancellationToken cancellationToken = default) => default;
            }

            [Decorator(Order = 1)]
            public class QueryDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IQuery<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """).ShouldCompile().Registrations;

        registrations.GetPipeline("global::GetName", "string").Should().Contain("QueryDecorator");
        registrations.TryGetPipeline("global::GetCount", "string").Should().BeNull();
    }

    [Fact]
    public void GenericInterfaceConstraint_IsSerializedWithPlaceholders()
    {
        var result = RunGenerator("""
            using DecoratR;

            [assembly: DecoratR.GenerateDecoratRMetadata]

            public interface IQuery<TResult> : IRequest;

            [Decorator(Order = 1)]
            public class QueryDecorator<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> inner) : IRequestHandler<TRequest, TResponse>
                where TRequest : IQuery<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
                    => inner.HandleAsync(request, cancellationToken);
            }
            """, "HandlerLib").ShouldCompile();

        result.DecoratorRegistry.Should().Contain("RequestConstraints = \"global::IQuery<{TResponse}>\"");
        result.DecoratorRegistry.GetApplyMethod("ApplyQueryDecorator").Should().Contain("where TRequest : global::IQuery<TResponse>");
    }

    [Fact]
    public void CrossAssembly_ConstrainedDecoratorAppliedOnlyToMatchingHandlers()
    {
        var (_, host) = RunTwoStageGenerator(
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
            TestSources.EmptyHost());

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::RemoteCommand", "string").Should().Contain("ApplyCommandDecorator");
        host.Registrations.TryGetPipeline("global::RemoteQuery", "string").Should().BeNull();
    }

    [Fact]
    public void CrossAssembly_ApplyMethodHasSpecificConstraint()
    {
        var result = RunGenerator($$"""
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
            """, "HandlerLib").ShouldCompile();

        var applyMethod = result.DecoratorRegistry.GetApplyMethod("ApplyCommandDecorator");
        applyMethod.Should().Contain("where TRequest : global::ICommand\n");
        applyMethod.Should().NotContain("where TRequest : global::DecoratR.IRequest");

        result.DecoratorRegistry.Should().Contain("RequestConstraints = \"global::ICommand\"");
    }

    [Fact]
    public void CrossAssembly_ConstrainedDecoratorCorrectlyFiltersLocalHandlers()
    {
        var (_, host) = RunTwoStageGenerator(
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

        host.ShouldCompile();
        host.Registrations.GetPipeline("global::RemoteCommand", "string").Should().Contain("ApplyCommandDecorator");
        host.Registrations.TryGetPipeline("global::LocalQuery", "string").Should().BeNull();
    }
}
