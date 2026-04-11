namespace DecoratR.Sample.Application.Validation;

public sealed class ValidationException(IReadOnlyList<ValidationFailure> failures)
    : Exception("Validation failed.")
{
    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;
}