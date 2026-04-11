using DecoratR.Sample.Application.Validation;

namespace DecoratR.Sample.Application.Greetings.Commands;

internal sealed class GreetCommandValidator : AbstractValidator<GreetCommand>
{
    public GreetCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaxLength(100);
    }
}