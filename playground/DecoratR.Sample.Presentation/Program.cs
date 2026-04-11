using DecoratR.Sample.Application;
using DecoratR.Sample.Infrastructure;
using DecoratR.Sample.Presentation;
using DecoratR.Sample.Presentation.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(DefaultSerializerContext.Default));

builder.Services.AddDecoratR();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGreetEndpoint();
app.MapGetGreetingEndpoint();
app.MapGetGreetingsStreamEndpoint();

app.Run();