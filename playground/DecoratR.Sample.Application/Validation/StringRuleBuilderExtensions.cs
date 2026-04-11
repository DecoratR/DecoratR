namespace DecoratR.Sample.Application.Validation;

public static class StringRuleBuilderExtensions
{
    extension<T>(RuleBuilder<T, string> builder)
    {
        public RuleBuilder<T, string> NotEmpty()
        {
            return builder.Must(v => !string.IsNullOrWhiteSpace(v), $"'{builder.PropertyName}' must not be empty.");
        }

        public RuleBuilder<T, string> MinLength(int min)
        {
            return builder.Must(v => v.Length >= min, $"'{builder.PropertyName}' must be at least {min} characters.");
        }

        public RuleBuilder<T, string> MaxLength(int max)
        {
            return builder.Must(v => v.Length <= max, $"'{builder.PropertyName}' must not exceed {max} characters.");
        }
    }
}