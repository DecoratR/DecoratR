using DecoratR;
using Examples.ModularMonolith.SharedKernel.Messaging;
using Examples.ModularMonolith.SharedKernel.Validation;

namespace Examples.ModularMonolith.SharedKernel.Decorators;

/// <summary>Runs every registered <see cref="IValidator{TRequest}"/> before a command reaches its handler.</summary>
[Decorator(Order = 10)]
internal sealed class ValidationDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IEnumerable<IValidator<TRequest>> validators)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand
{
    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var errors = validators.SelectMany(v => v.Validate(request)).ToList();
        if (errors.Count > 0) throw new ValidationException(errors);

        return inner.HandleAsync(request, cancellationToken);
    }
}
