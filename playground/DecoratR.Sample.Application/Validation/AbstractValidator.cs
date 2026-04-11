using System.Runtime.CompilerServices;

namespace DecoratR.Sample.Application.Validation;

public abstract class AbstractValidator<T> : IValidator<T>
{
    private readonly List<Func<T, IEnumerable<ValidationFailure>>> _rules = [];

    public ValidationResult Validate(T instance)
    {
        List<ValidationFailure>? failures = null;

        foreach (var failure in _rules.SelectMany(rule => rule(instance)))
        {
            failures ??= [];
            failures.Add(failure);
        }

        return failures is null ? ValidationResult.Success : new ValidationResult(failures);
    }

    protected RuleBuilder<T, TProperty> RuleFor<TProperty>(
        Func<T, TProperty> getter,
        [CallerArgumentExpression(nameof(getter))]
        string? expression = null)
    {
        var propertyName = ParsePropertyName(expression);
        var builder = new RuleBuilder<T, TProperty>(propertyName, getter);
        _rules.Add(builder.Validate);
        return builder;
    }

    private static string ParsePropertyName(string? expression)
    {
        if (string.IsNullOrEmpty(expression))
            return "Unknown";

        // Parses "x => x.PropertyName" → "PropertyName"
        var dotIndex = expression.LastIndexOf('.');
        return dotIndex >= 0 ? expression[(dotIndex + 1)..].Trim() : expression.Trim();
    }
}