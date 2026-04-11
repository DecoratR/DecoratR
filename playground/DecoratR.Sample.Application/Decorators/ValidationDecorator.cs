using DecoratR.Sample.Application.Abstractions;
using DecoratR.Sample.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Sample.Application.Decorators;

[Decorator(Order = 4)]
internal sealed class ValidationDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    IServiceProvider serviceProvider)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICommand
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var validator = serviceProvider.GetService<IValidator<TRequest>>();

        if (validator is not null)
        {
            var result = validator.Validate(request);
            if (!result.IsValid)
                throw new ValidationException(result.Failures);
        }

        return await inner.HandleAsync(request, cancellationToken);
    }
}