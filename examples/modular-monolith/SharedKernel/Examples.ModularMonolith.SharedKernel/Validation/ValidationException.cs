namespace Examples.ModularMonolith.SharedKernel.Validation;

public sealed class ValidationException(IReadOnlyList<string> errors)
    : Exception($"Validation failed: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
