namespace DecoratR.Sample.Application.Validation;

public sealed class ValidationResult(IReadOnlyList<ValidationFailure> failures)
{
    public static readonly ValidationResult Success = new([]);

    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;

    public bool IsValid => Failures.Count == 0;
}