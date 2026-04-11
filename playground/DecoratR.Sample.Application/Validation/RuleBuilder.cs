namespace DecoratR.Sample.Application.Validation;

public sealed class RuleBuilder<T, TProperty>
{
    private readonly List<(Func<TProperty, bool> check, string message)> _checks = [];
    private readonly Func<T, TProperty> _getter;

    internal RuleBuilder(string propertyName, Func<T, TProperty> getter)
    {
        PropertyName = propertyName;
        _getter = getter;
    }

    public string PropertyName { get; }

    public RuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string errorMessage)
    {
        _checks.Add((predicate, errorMessage));
        return this;
    }

    public RuleBuilder<T, TProperty> NotNull()
    {
        _checks.Add((v => v is not null, $"'{PropertyName}' must not be null."));
        return this;
    }

    internal IEnumerable<ValidationFailure> Validate(T instance)
    {
        var value = _getter(instance);
        foreach (var (check, message) in _checks)
            if (!check(value))
                yield return new ValidationFailure(PropertyName, message);
    }
}