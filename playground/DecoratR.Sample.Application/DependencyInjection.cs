using DecoratR.Sample.Application.Greetings.Commands;
using DecoratR.Sample.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Sample.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<GreetCommand>, GreetCommandValidator>();
        return services;
    }
}