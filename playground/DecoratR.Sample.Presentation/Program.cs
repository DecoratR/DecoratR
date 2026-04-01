using DecoratR;
using DecoratR.Generated;
using DecoratR.Sample.Application;
using DecoratR.Sample.Infrastructure;
using DecoratR.Sample.Presentation.Decorators;
using DecoratR.Sample.Presentation.Endpoints;
using FluentValidation;
using Scalar.AspNetCore;
using ServiceLifetime = DecoratR.ServiceLifetime;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDecoratR(options => options
        .RegisterHandlers()
        .AddDecorator(typeof(ExceptionHandlingDecorator<,>))
        .AddDecorator(typeof(RequestLoggingDecorator<,>))
        .AddDecorator(typeof(PerformanceLoggingDecorator<,>))
        .AddDecorator(typeof(ValidationDecorator<,>))
        .WithLifetime(ServiceLifetime.Scoped));

builder.Services.AddValidatorsFromAssembly(ApplicationAssembly.Assembly);

builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGreetEndpoint();
app.MapGetGreetingEndpoint();

app.Run();