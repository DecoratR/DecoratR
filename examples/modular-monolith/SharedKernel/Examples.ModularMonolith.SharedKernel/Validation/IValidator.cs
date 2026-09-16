namespace Examples.ModularMonolith.SharedKernel.Validation;

/// <summary>Validates a request. Implementations are registered by the module that owns the request.</summary>
public interface IValidator<in TRequest>
{
    IReadOnlyList<string> Validate(TRequest request);
}
