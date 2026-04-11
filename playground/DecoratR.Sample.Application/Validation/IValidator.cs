namespace DecoratR.Sample.Application.Validation;

public interface IValidator<in T>
{
    ValidationResult Validate(T instance);
}