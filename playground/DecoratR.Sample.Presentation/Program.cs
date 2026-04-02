using DecoratR.Sample.Application;
using DecoratR.Sample.Infrastructure;
using DecoratR.Sample.Presentation;
using DecoratR.Sample.Presentation.Endpoints;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Reflection-free: handlers and decorators discovered at compile-time by the DecoratR source generator.
// For custom decorator ordering, use: .AddDecoratR(options => options.AddHandlers(...).AddDecorator(...))
builder.Services.AddDecoratR();

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