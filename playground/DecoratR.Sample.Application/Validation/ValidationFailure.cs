namespace DecoratR.Sample.Application.Validation;

public sealed class ValidationFailure(string propertyName, string errorMessage)
{
    public string PropertyName { get; } = propertyName;
    public string ErrorMessage { get; } = errorMessage;
}