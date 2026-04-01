using FluentValidation;

namespace DecoratR.Sample.Application.Greetings.Commands;

public sealed class GreetCommandValidator : AbstractValidator<GreetCommand>
{
    public GreetCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Please specify a name.");
    }
}